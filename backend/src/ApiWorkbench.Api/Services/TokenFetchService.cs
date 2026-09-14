using System.Net.Http.Headers;
using System.Text.Json;
using ApiWorkbench.Api.Configuration;
using ApiWorkbench.Api.Contracts;
using ApiWorkbench.Api.Data;
using ApiWorkbench.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ApiWorkbench.Api.Services;

public interface ITokenFetchService
{
    Task<FetchTokenResponse> FetchAsync(int serviceId, string environment, string? regionCode, CancellationToken cancellationToken = default);
}

public sealed class TokenFetchService(
    AppDbContext db,
    IConfiguration configuration,
    IHostEnvironment hostEnvironment,
    ILogger<TokenFetchService> logger) : ITokenFetchService
{
    public async Task<FetchTokenResponse> FetchAsync(
        int serviceId,
        string environment,
        string? regionCode,
        CancellationToken cancellationToken = default)
    {
        var env = environment.Trim().ToLowerInvariant();
        if (!ServiceEnvironments.All.Contains(env))
        {
            throw new InvalidOperationException($"Неизвестная среда '{environment}'.");
        }

        var service = await db.Services
            .AsNoTracking()
            .Include(s => s.TokenUrls)
            .Include(s => s.Urls)
            .FirstOrDefaultAsync(s => s.Id == serviceId && s.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("Сервис не найден.");

        if (!string.Equals(service.AuthType, "token", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("У сервиса auth.type не token.");
        }

        var region = service.IsRegional ? (regionCode ?? service.DefaultRegion ?? string.Empty).Trim().ToLowerInvariant() : string.Empty;
        var tokenUrl = service.TokenUrls.FirstOrDefault(u =>
            string.Equals(u.Environment, env, StringComparison.OrdinalIgnoreCase)
            && string.Equals(u.RegionCode ?? string.Empty, region, StringComparison.OrdinalIgnoreCase))
            ?? service.TokenUrls.FirstOrDefault(u =>
                string.Equals(u.Environment, env, StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrEmpty(u.RegionCode));

        if (tokenUrl is null || string.IsNullOrWhiteSpace(tokenUrl.Url))
        {
            throw new InvalidOperationException($"Нет auth.token_url для среды '{env}'.");
        }

        var target = ResolveTarget(tokenUrl.Url, service, env, region);
        if (!string.Equals(target.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("token_url должен быть HTTPS: токен берётся с клиентским сертификатом.");
        }

        if (!ClientCertLocator.NeedsClientCertificate(service))
        {
            throw new InvalidOperationException(
                "Token fetch needs a client certificate: auth.cert_path, auth.cert_base64, or auth.cert_vault.");
        }

        var (client, dispose) = CreateClient(service);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, target);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Токен HTTP {(int)response.StatusCode}: {Truncate(body)}");
            }

            var token = ReadAccessToken(body, service.TokenField);
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("В ответе нет accessToken.");
            }

            logger.LogInformation("Токен для {Service} / {Env} получен", service.Name, env);
            return new FetchTokenResponse(token);
        }
        finally
        {
            if (dispose)
            {
                client.Dispose();
            }
        }
    }

    private (HttpClient Client, bool Dispose) CreateClient(ServiceEntity service)
    {
        var handler = ClientCertLocator.CreateHandler(service, configuration, hostEnvironment);
        return (new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) }, true);
    }

    private static Uri ResolveTarget(string tokenUrl, ServiceEntity service, string env, string region)
    {
        var raw = tokenUrl.Trim();
        if (Uri.TryCreate(raw, UriKind.Absolute, out var absolute)
            && absolute.Scheme is "http" or "https")
        {
            return absolute;
        }

        var baseUrl = service.Urls.FirstOrDefault(u =>
            string.Equals(u.Environment, env, StringComparison.OrdinalIgnoreCase)
            && string.Equals(u.RegionCode ?? string.Empty, region, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrEmpty(u.Module))?.BaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var parent))
        {
            throw new InvalidOperationException("token_url относительный, но нет base URL сервиса.");
        }

        return new Uri(parent, raw.TrimStart('/'));
    }

    private static string? ReadAccessToken(string json, string? preferredField)
    {
        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            root = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Ответ токена не JSON.");
        }

        var names = new List<string>();
        if (!string.IsNullOrWhiteSpace(preferredField))
        {
            names.Add(preferredField.Trim());
        }

        names.AddRange(["accessToken", "access_token", "token"]);
        foreach (var name in names.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.String)
                {
                    var value = property.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }
        }

        return null;
    }

    private static string Truncate(string value) =>
        value.Length <= 240 ? value : value[..240] + "…";
}
