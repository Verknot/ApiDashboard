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
    Task<FetchTokenResponse> FetchAsync(
        int serviceId,
        string environment,
        string? regionCode,
        string? module,
        CancellationToken cancellationToken = default);
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
        string? module,
        CancellationToken cancellationToken = default)
    {
        var env = environment.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(env))
        {
            throw new InvalidOperationException("Environment is required.");
        }

        _ = ServiceEnvironments.Normalize(env);

        var service = await db.Services
            .AsNoTracking()
            .Include(s => s.TokenUrls)
            .Include(s => s.Urls)
            .Include(s => s.SwaggerSources)
            .FirstOrDefaultAsync(s => s.Id == serviceId && s.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("Сервис не найден.");

        var moduleName = module?.Trim() ?? string.Empty;
        var auth = ServiceAuthResolver.Resolve(service, moduleName);
        if (!string.Equals(auth.AuthType, "token", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                string.IsNullOrEmpty(moduleName)
                    ? "У сервиса auth.type не token."
                    : $"У portal '{moduleName}' auth.type не token.");
        }

        var region = service.IsRegional
            ? NormalizeRegionCode(regionCode ?? service.DefaultRegion)
            : string.Empty;
        var tokenUrl = PickTokenUrl(service, env, region, moduleName);
        if (tokenUrl is null || string.IsNullOrWhiteSpace(tokenUrl.Url))
        {
            throw new InvalidOperationException(
                string.IsNullOrEmpty(moduleName)
                    ? $"Нет auth.token_url для среды '{env}'."
                    : $"Нет auth.token_url у portal '{moduleName}' для среды '{env}'.");
        }

        var target = ResolveTarget(tokenUrl.Url, service, env, region, moduleName);
        if (!string.Equals(target.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("token_url должен быть HTTPS: токен берётся с клиентским сертификатом.");
        }

        if (!auth.NeedsClientCertificate)
        {
            throw new InvalidOperationException(
                "Token fetch needs a client certificate: auth.cert, cert_base64, or cert_vault.");
        }

        var (client, dispose) = CreateClient(auth, service.Name);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, target);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var code = (int)response.StatusCode;
            if (code is >= 300 and < 400)
            {
                var location = response.Headers.Location;
                var redirect = location is null
                    ? null
                    : (location.IsAbsoluteUri ? location : new Uri(target, location)).ToString();
                if (string.IsNullOrWhiteSpace(redirect))
                {
                    throw new InvalidOperationException($"Токен HTTP {code}: redirect without Location.");
                }

                logger.LogInformation("Токен для {Service}/{Module} / {Env} → redirect", service.Name, moduleName, env);
                return new FetchTokenResponse(null, redirect);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Токен HTTP {code}: {Truncate(body)}");
            }

            var token = ReadAccessToken(body, auth.TokenField);
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("В ответе нет accessToken.");
            }

            logger.LogInformation("Токен для {Service}/{Module} / {Env} получен", service.Name, moduleName, env);
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

    private static ServiceTokenUrl? PickTokenUrl(ServiceEntity service, string env, string region, string module)
    {
        ServiceTokenUrl? Match(string moduleFilter) =>
            service.TokenUrls.FirstOrDefault(u =>
                string.Equals(u.Environment, env, StringComparison.OrdinalIgnoreCase)
                && string.Equals(u.RegionCode ?? string.Empty, region, StringComparison.OrdinalIgnoreCase)
                && string.Equals(u.Module ?? string.Empty, moduleFilter, StringComparison.OrdinalIgnoreCase))
            ?? service.TokenUrls.FirstOrDefault(u =>
                string.Equals(u.Environment, env, StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrEmpty(u.RegionCode)
                && string.Equals(u.Module ?? string.Empty, moduleFilter, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(module))
        {
            return Match(module) ?? Match(string.Empty);
        }

        return Match(string.Empty);
    }

    private (HttpClient Client, bool Dispose) CreateClient(ServiceAuthContext auth, string serviceName)
    {
        var handler = ClientCertLocator.CreateHandler(auth, serviceName, configuration, hostEnvironment);
        return (new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) }, true);
    }

    private static Uri ResolveTarget(string tokenUrl, ServiceEntity service, string env, string region, string module)
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
            && string.Equals(u.Module ?? string.Empty, module, StringComparison.OrdinalIgnoreCase))?.BaseUrl
            ?? service.Urls.FirstOrDefault(u =>
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

    private static string NormalizeRegionCode(string? code)
    {
        var value = (code ?? string.Empty).Trim().ToLowerInvariant();
        return value is "global" or "none" or "no" or "-" or "_" ? string.Empty : value;
    }

    private static string Truncate(string value) =>
        value.Length <= 240 ? value : value[..240] + "…";
}
