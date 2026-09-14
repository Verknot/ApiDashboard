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
        var (client, disposeClient) = CreateClient(service, auth);
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
            return new ProxySendResponse(null, (int)clock.ElapsedMilliseconds, ex.Message, ex.Message, new Dictionary<string, string>());
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
        if (service is null || auth is null || !auth.NeedsClientCertificate)
        {
            return (httpClientFactory.CreateClient("relay"), false);
        }

        var handler = ClientCertLocator.CreateHandler(auth, service.Name, configuration, hostEnvironment);
        return (new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) }, true);
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

    private static Dictionary<string, string> CollectHeaders(HttpResponseMessage response)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void Add(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
        {
            foreach (var header in headers)
            {
                var value = string.Join(", ", header.Value);
                if (map.TryGetValue(header.Key, out var existing))
                {
                    map[header.Key] = $"{existing}, {value}";
                }
                else
                {
                    map[header.Key] = value;
                }
            }
        }

        Add(response.Headers);
        Add(response.Content.Headers);
        try
        {
            Add(response.TrailingHeaders);
        }
        catch (NotSupportedException)
        {
            // Some handlers do not support trailing headers.
        }

        // Plain dictionary: OrdinalIgnoreCase comparer can confuse some JSON serializers.
        return new Dictionary<string, string>(map, StringComparer.Ordinal);
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
