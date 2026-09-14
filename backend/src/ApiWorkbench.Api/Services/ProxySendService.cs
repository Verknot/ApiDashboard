using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;
using ApiWorkbench.Api.Configuration;
using ApiWorkbench.Api.Contracts;
using ApiWorkbench.Api.Data;
using ApiWorkbench.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApiWorkbench.Api.Services;

public interface IProxySendService
{
    Task<ProxySendResponse> SendAsync(ProxySendRequest request, CancellationToken cancellationToken = default);
}

public sealed class ProxySendService(
    AppDbContext db,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IHostEnvironment hostEnvironment,
    IOptions<HistoryOptions> historyOptions) : IProxySendService
{
    private static readonly HashSet<string> AllowedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"
    };

    private static readonly HashSet<string> DeniedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host", "Cookie", "Cookie2", "Content-Length", "Connection", "Keep-Alive",
        "Proxy-Connection", "Transfer-Encoding", "TE", "Trailer", "Upgrade", "Expect"
    };

    public async Task<ProxySendResponse> SendAsync(ProxySendRequest request, CancellationToken cancellationToken = default)
    {
        var method = (request.Method ?? "").Trim().ToUpperInvariant();
        if (!AllowedMethods.Contains(method))
        {
            throw new InvalidOperationException($"Method {request.Method} is not supported.");
        }

        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var target)
            || target.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("URL must be absolute http/https.");
        }

        ServiceEntity? service = null;
        if (request.ServiceId is int serviceId)
        {
            service = await db.Services.AsNoTracking()
                .Include(s => s.Urls)
                .Include(s => s.SwaggerSources)
                .FirstOrDefaultAsync(s => s.Id == serviceId && s.IsActive, cancellationToken);
            if (service is null)
            {
                throw new KeyNotFoundException("Service not found.");
            }

            if (!IsAllowedTarget(target, service.Urls.Select(u => u.BaseUrl)))
            {
                throw new InvalidOperationException("URL host/port must match this service catalog.");
            }
        }

        using var message = new HttpRequestMessage(new HttpMethod(method), target);
        string? contentType = null;
        foreach (var (key, value) in request.Headers ?? [])
        {
            if (string.IsNullOrWhiteSpace(key) || DeniedHeaders.Contains(key))
            {
                continue;
            }

            if (key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                contentType = value;
                continue;
            }

            message.Headers.TryAddWithoutValidation(key, value);
        }

        if (HasBody(method) && request.Body is not null)
        {
            message.Content = new StringContent(request.Body, Encoding.UTF8);
            if (!string.IsNullOrWhiteSpace(contentType)
                && MediaTypeHeaderValue.TryParse(contentType, out var parsed))
            {
                message.Content.Headers.ContentType = parsed;
            }
            else
            {
                message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            }
        }

        var module = service is null ? null : ServiceAuthResolver.MatchModuleByUrl(service, target);
        var auth = service is null ? null : ServiceAuthResolver.Resolve(service, module);
        HttpClient client;
        bool disposeClient;
        try
        {
            (client, disposeClient) = CreateClient(service, auth);
        }
        catch (InvalidOperationException ex)
        {
            return new ProxySendResponse(null, 0, ex.Message, ex.Message, []);
        }

        var clock = Stopwatch.StartNew();
        try
        {
            using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            clock.Stop();
            return new ProxySendResponse(
                (int)response.StatusCode,
                (int)clock.ElapsedMilliseconds,
                Truncate(raw, historyOptions.Value.MaxResponseBodyBytes),
                null,
                CollectHeaders(response));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or AuthenticationException)
        {
            clock.Stop();
            var detail = DescribeSendFailure(ex, service, auth);
            return new ProxySendResponse(null, (int)clock.ElapsedMilliseconds, detail, detail, []);
        }
        finally
        {
            if (disposeClient)
            {
                client.Dispose();
            }
        }
    }

    private (HttpClient Client, bool Dispose) CreateClient(ServiceEntity? service, ServiceAuthContext? auth)
    {
        if (service is null || auth is null)
        {
            return (httpClientFactory.CreateClient("relay"), false);
        }

        if (string.Equals(auth.AuthType, "certificate", StringComparison.OrdinalIgnoreCase)
            && !auth.HasClientCertificateMaterial)
        {
            throw new InvalidOperationException(
                $"Service '{service.Name}'{(string.IsNullOrEmpty(auth.Module) ? "" : "/" + auth.Module)} needs a client certificate: set api_auth.cert_path / cert_base64 / cert_vault (PFX in C:\\pult-certs).");
        }

        if (!auth.NeedsClientCertificate)
        {
            return (httpClientFactory.CreateClient("relay"), false);
        }

        var handler = ClientCertLocator.CreateHandler(auth, service.Name, configuration, hostEnvironment);
        return (new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) }, true);
    }

    private static string DescribeSendFailure(Exception ex, ServiceEntity? service, ServiceAuthContext? auth)
    {
        var text = ex.ToString();
        if (text.Contains("certificate required", StringComparison.OrdinalIgnoreCase)
            || text.Contains("tlsv13 alert certificate required", StringComparison.OrdinalIgnoreCase)
            || text.Contains("alert certificate required", StringComparison.OrdinalIgnoreCase))
        {
            var where = service is null
                ? "this URL"
                : $"'{service.Name}'{(auth is null || string.IsNullOrEmpty(auth.Module) ? "" : "/" + auth.Module)}";
            var hasCert = auth?.HasClientCertificateMaterial == true;
            return hasCert
                ? $"TLS: server requires a client certificate for {where}, but the presented PFX was rejected (wrong cert, expired, or password). Check C:\\pult-certs and api_auth.cert_path / cert_password."
                : $"TLS: server requires a client certificate (mTLS) for {where}. Set api_auth.type: certificate (or token) and api_auth.cert_path: your.pfx in C:\\pult-certs, then From disk / reload config.";
        }

        return ex.GetBaseException().Message;
    }

    private static bool HasBody(string method) => method is not ("GET" or "HEAD");

    private static bool IsAllowedTarget(Uri target, IEnumerable<string> baseUrls)
    {
        foreach (var raw in baseUrls)
        {
            if (!Uri.TryCreate(raw, UriKind.Absolute, out var allowed))
            {
                continue;
            }

            if (string.Equals(allowed.Scheme, target.Scheme, StringComparison.OrdinalIgnoreCase)
                && string.Equals(allowed.IdnHost, target.IdnHost, StringComparison.OrdinalIgnoreCase)
                && allowed.Port == target.Port)
            {
                return true;
            }
        }

        return false;
    }

    private static List<ResponseHeaderItem> CollectHeaders(HttpResponseMessage response)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void AddPair(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            if (map.TryGetValue(name, out var existing) && !string.IsNullOrEmpty(existing))
            {
                if (!existing.Contains(value, StringComparison.Ordinal))
                {
                    map[name] = $"{existing}, {value}";
                }
            }
            else
            {
                map[name] = value;
            }
        }

        void AddHeaders(System.Net.Http.Headers.HttpHeaders headers)
        {
            foreach (var header in headers)
            {
                try
                {
                    AddPair(header.Key, string.Join(", ", header.Value));
                }
                catch (InvalidOperationException)
                {
                    // Skip malformed typed headers; NonValidated below still captures raw values.
                }
            }

            foreach (var header in headers.NonValidated)
            {
                AddPair(header.Key, header.Value.ToString());
            }
        }

        AddHeaders(response.Headers);
        if (response.Content is not null)
        {
            AddHeaders(response.Content.Headers);
        }

        try
        {
            AddHeaders(response.TrailingHeaders);
        }
        catch (NotSupportedException)
        {
        }

        return map
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new ResponseHeaderItem(pair.Key, pair.Value))
            .ToList();
    }

    private static string Truncate(string body, int maxBytes)
    {
        maxBytes = Math.Max(1024, maxBytes);
        var bytes = Encoding.UTF8.GetByteCount(body);
        if (bytes <= maxBytes)
        {
            return body;
        }

        var buffer = Encoding.UTF8.GetBytes(body);
        return Encoding.UTF8.GetString(buffer, 0, maxBytes) + "\n… truncated";
    }
}
