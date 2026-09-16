using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using ApiWorkbench.Api.Configuration;
using ApiWorkbench.Api.Data;
using ApiWorkbench.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;

namespace ApiWorkbench.Api.Services;

public sealed class SwaggerServiceResult
{
    public required string Service { get; init; }
    public required string Status { get; init; }
    public int Endpoints { get; init; }
    public int Added { get; init; }
    public int Removed { get; init; }
    public int Changed { get; init; }
    public string? Error { get; init; }
}

public sealed class SwaggerRefreshResult
{
    public int Ok { get; init; }
    public int Failed { get; init; }
    public IReadOnlyList<SwaggerServiceResult> Services { get; init; } = [];
}

public interface ISwaggerIngestService
{
    Task<SwaggerRefreshResult> RefreshAllAsync(CancellationToken cancellationToken = default);
}

public sealed class SwaggerIngestService(
    AppDbContext db,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IHostEnvironment hostEnvironment,
    IOptions<VaultOptions> vaultOptions,
    ILogger<SwaggerIngestService> logger) : ISwaggerIngestService
{
    public async Task<SwaggerRefreshResult> RefreshAllAsync(CancellationToken cancellationToken = default)
    {
        var services = await db.Services
            .Where(s => s.IsActive)
            .Include(s => s.SwaggerSources)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

        var results = new List<SwaggerServiceResult>();
        foreach (var service in services)
        {
            results.Add(await RefreshOneAsync(service, cancellationToken));
        }

        return new SwaggerRefreshResult
        {
            Ok = results.Count(r => r.Status == "ok"),
            Failed = results.Count(r => r.Status != "ok"),
            Services = results
        };
    }

    private async Task<SwaggerServiceResult> RefreshOneAsync(ServiceEntity service, CancellationToken cancellationToken)
    {
        var sources = service.SwaggerSources
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .ToList();
        if (sources.Count == 0 && !string.IsNullOrWhiteSpace(service.SwaggerUrl))
        {
            sources.Add(new ServiceSwaggerSource
            {
                Name = string.Empty,
                Url = service.SwaggerUrl,
                AuthType = service.SwaggerAuthType,
                VaultPath = service.SwaggerVaultPath,
                VaultUsernamePath = service.SwaggerVaultUsernamePath,
                VaultPasswordPath = service.SwaggerVaultPasswordPath,
                VaultBase64 = service.SwaggerVaultBase64,
                BasicUsername = service.SwaggerBasicUsername,
                BasicPassword = service.SwaggerBasicPassword
            });
        }

        if (sources.Count == 0 || sources.All(s => string.IsNullOrWhiteSpace(s.Url)))
        {
            return Fail(service.Name, "В конфиге нет swagger.url");
        }

        var existing = await db.Endpoints.Where(e => e.ServiceId == service.Id).ToListAsync(cancellationToken);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var configuredModules = new HashSet<string>(
            sources.Select(s => s.Name ?? string.Empty),
            StringComparer.OrdinalIgnoreCase);
        var refreshedModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        var total = 0;
        var added = 0;
        var changed = 0;
        var okModules = 0;

        foreach (var source in sources.Where(s => !string.IsNullOrWhiteSpace(s.Url)))
        {
            var module = source.Name?.Trim() ?? string.Empty;
            var label = string.IsNullOrEmpty(module) ? service.Name : $"{service.Name}/{module}";
            try
            {
                var text = await LoadDocumentAsync(service, source, cancellationToken);
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
                var document = new OpenApiStreamReader().Read(stream, out var diagnostic);
                if (document.Paths is null || document.Paths.Count == 0)
                {
                    var hint = diagnostic.Errors.Count > 0
                        ? string.Join("; ", diagnostic.Errors.Select(e => e.Message))
                        : "В спецификации нет paths";
                    errors.Add($"{label}: {hint}");
                    continue;
                }

                // Store as JSON in jsonb; OpenAPI YAML is accepted on download and converted here.
                using var raw = ToJsonDocument(text, document);

                var previousSnapshot = await db.ContractSnapshots
                    .AsNoTracking()
                    .Where(s => s.ServiceId == service.Id && s.Module == module)
                    .OrderByDescending(s => s.FetchedAt)
                    .ThenByDescending(s => s.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                var diff = previousSnapshot is null
                    ? new ContractDiffCounts([], [], [])
                    : ContractDiffService.CompareDocuments(previousSnapshot.RawJson, raw);

                db.ContractSnapshots.Add(new ContractSnapshot
                {
                    ServiceId = service.Id,
                    Module = module,
                    FetchedAt = DateTimeOffset.UtcNow,
                    RawJson = JsonDocument.Parse(raw.RootElement.GetRawText())
                });

                var parsed = ParseEndpoints(service.Id, module, document);
                total += parsed.Count;
                added += diff.Added.Count;
                changed += diff.Changed.Count;

                foreach (var item in parsed)
                {
                    var key = EndpointKey(module, item.Method, item.Path);
                    seen.Add(key);
                    var row = existing.FirstOrDefault(e =>
                        string.Equals(e.Module ?? string.Empty, module, StringComparison.OrdinalIgnoreCase)
                        && e.Method.Equals(item.Method, StringComparison.OrdinalIgnoreCase)
                        && e.Path.Equals(item.Path, StringComparison.Ordinal));
                    if (row is null)
                    {
                        db.Endpoints.Add(item);
                    }
                    else
                    {
                        row.Description = item.Description;
                        row.OperationId = item.OperationId;
                        row.RequestSchema = item.RequestSchema;
                        row.ResponseSchema = item.ResponseSchema;
                        row.Parameters = item.Parameters;
                        row.Tags = item.Tags;
                        row.Module = module;
                    }
                }

                okModules++;
                refreshedModules.Add(module);
                logger.LogInformation(
                    "Swagger {Service}: {Count} эндпоинтов, +{Added} -{Removed} ~{Changed}",
                    label,
                    parsed.Count,
                    diff.Added.Count,
                    diff.Removed.Count,
                    diff.Changed.Count);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Не удалось стянуть swagger {Service}", label);
                errors.Add($"{label}: {ex.Message}");
            }
        }

        var stale = existing.Where(e =>
        {
            var module = e.Module ?? string.Empty;
            if (!configuredModules.Contains(module))
            {
                return true;
            }

            return refreshedModules.Contains(module) && !seen.Contains(EndpointKey(module, e.Method, e.Path));
        }).ToList();

        db.Endpoints.RemoveRange(stale);
        await db.SaveChangesAsync(cancellationToken);

        if (okModules == 0)
        {
            return Fail(service.Name, errors.Count == 0 ? "Не удалось стянуть swagger" : string.Join(" | ", errors));
        }

        return new SwaggerServiceResult
        {
            Service = service.Name,
            Status = errors.Count == 0 ? "ok" : "error",
            Endpoints = total,
            Added = added,
            Removed = stale.Count,
            Changed = changed,
            Error = errors.Count == 0 ? null : string.Join(" | ", errors)
        };
    }

    private static string EndpointKey(string module, string method, string path) =>
        $"{module}:{method}:{path}";

    private async Task<string> LoadDocumentAsync(ServiceEntity service, ServiceSwaggerSource source, CancellationToken cancellationToken)
    {
        var swaggerUrl = source.Url.Trim();
        if (IsLocalSource(swaggerUrl))
        {
            var yamlPath = ServicesYamlLocator.Resolve(configuration, hostEnvironment);
            var yamlDir = Path.GetDirectoryName(yamlPath) ?? Directory.GetCurrentDirectory();
            var full = Path.GetFullPath(Path.Combine(yamlDir, swaggerUrl.Replace("file:", "", StringComparison.OrdinalIgnoreCase).Trim()));
            if (!File.Exists(full))
            {
                throw new FileNotFoundException($"Локальный swagger не найден: {full}");
            }

            return await File.ReadAllTextAsync(full, Encoding.UTF8, cancellationToken);
        }

        var (client, dispose) = CreateSwaggerClient(service, source);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, swaggerUrl);
            if (string.Equals(source.AuthType, "basic", StringComparison.OrdinalIgnoreCase))
            {
                var basic = await ResolveBasicAsync(source, cancellationToken);
                if (basic is null)
                {
                    throw new InvalidOperationException("Для swagger basic укажите username/password или vault_username/vault_password.");
                }

                var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{basic.Value.User}:{basic.Value.Password}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
            }

            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"HTTP {(int)response.StatusCode}: {Truncate(body)}");
            }

            return body;
        }
        catch (Exception ex) when (ex is HttpRequestException or AuthenticationException)
        {
            if (ClientCertLocator.NeedsClientCertificate(ServiceAuthResolver.Resolve(service, source.Name)))
            {
                throw new InvalidOperationException(
                    $"Swagger {service.Name}: TLS/сеть. Нужны HTTPS и PFX из C:\\pult-certs" +
                    (source.Insecure ? "." : ", или swagger.insecure: true для внутреннего CA.") +
                    $" {ex.Message}",
                    ex);
            }

            if (!source.Insecure)
            {
                throw new InvalidOperationException(
                    $"Swagger {service.Name}: TLS. Поставьте CA в Trusted Root или swagger.insecure: true. {ex.Message}",
                    ex);
            }

            throw;
        }
        finally
        {
            if (dispose)
            {
                client.Dispose();
            }
        }
    }

    private (HttpClient Client, bool Dispose) CreateSwaggerClient(ServiceEntity service, ServiceSwaggerSource source)
    {
        var auth = ServiceAuthResolver.Resolve(service, source.Name);
        if (auth.NeedsClientCertificate)
        {
            var handler = ClientCertLocator.CreateHandler(auth, service.Name, configuration, hostEnvironment);
            if (source.Insecure)
            {
                ClientCertLocator.AllowInsecureServerCertificate(handler);
            }

            return (new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) }, true);
        }

        if (source.Insecure)
        {
            return (new HttpClient(ClientCertLocator.CreateInsecureRelayHandler())
            {
                Timeout = TimeSpan.FromSeconds(20)
            }, true);
        }

        return (httpClientFactory.CreateClient("swagger"), false);
    }

    private async Task<(string User, string Password)?> ResolveBasicAsync(ServiceSwaggerSource source, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(source.BasicUsername) || !string.IsNullOrWhiteSpace(source.BasicPassword))
        {
            if (string.IsNullOrWhiteSpace(source.BasicUsername) || string.IsNullOrWhiteSpace(source.BasicPassword))
            {
                throw new InvalidOperationException("Для swagger basic укажите и username, и password.");
            }

            return DecodePair(source.BasicUsername, source.BasicPassword, source.VaultBase64);
        }

        return await ReadVaultBasicAsync(source, cancellationToken);
    }

    private async Task<(string User, string Password)?> ReadVaultBasicAsync(ServiceSwaggerSource source, CancellationToken cancellationToken)
    {
        var options = vaultOptions.Value;
        if (string.IsNullOrWhiteSpace(options.Address) || string.IsNullOrWhiteSpace(options.Token))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var client = new VaultClient(new VaultClientSettings(options.Address, new TokenAuthMethodInfo(options.Token)));

        var usernameRef = source.VaultUsernamePath;
        var passwordRef = source.VaultPasswordPath;
        if (!string.IsNullOrWhiteSpace(usernameRef) || !string.IsNullOrWhiteSpace(passwordRef))
        {
            if (string.IsNullOrWhiteSpace(usernameRef) || string.IsNullOrWhiteSpace(passwordRef))
            {
                throw new InvalidOperationException("Для swagger basic нужны и vault_username, и vault_password.");
            }

            var user = await ReadVaultValueAsync(client, usernameRef, cancellationToken, UsernameKeys);
            var password = await ReadVaultValueAsync(client, passwordRef, cancellationToken, PasswordKeys);
            return DecodePair(user, password, source.VaultBase64);
        }

        if (string.IsNullOrWhiteSpace(source.VaultPath))
        {
            return null;
        }

        var data = await ReadVaultDataAsync(client, source.VaultPath, cancellationToken);
        if (data is null)
        {
            return null;
        }

        var extracted = ExtractBasic(data);
        return extracted is null ? null : DecodePair(extracted.Value.User, extracted.Value.Password, source.VaultBase64);
    }

    private static readonly string[] UsernameKeys =
    [
        "SwaggerBasicAuthUsername",
        "username",
        "user",
        "login",
        "value"
    ];

    private static readonly string[] PasswordKeys =
    [
        "SwaggerBasicAuthPassword",
        "password",
        "pass",
        "value"
    ];

    private async Task<string?> ReadVaultValueAsync(
        VaultClient client,
        string reference,
        CancellationToken cancellationToken,
        params string[] fallbackKeys)
    {
        var (path, key) = SplitSecretRef(reference);
        var data = await ReadVaultDataAsync(client, path, cancellationToken);
        return data is null ? null : ExtractValue(data, key, fallbackKeys);
    }

    private async Task<IDictionary<string, object>?> ReadVaultDataAsync(
        VaultClient client,
        string vaultPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (mount, path) = SplitVaultPath(vaultPath);
        try
        {
            var secret = await client.V1.Secrets.KeyValue.V2.ReadSecretAsync(path: path, mountPoint: mount);
            return secret.Data.Data;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Vault KV v2 {Path} недоступен, пробую v1", vaultPath);
            var secret = await client.V1.Secrets.KeyValue.V1.ReadSecretAsync(path: path, mountPoint: mount);
            return secret.Data;
        }
    }

    private static (string Path, string? Key) SplitSecretRef(string reference)
    {
        var trimmed = reference.Trim();
        var hash = trimmed.LastIndexOf('#');
        if (hash <= 0 || hash == trimmed.Length - 1)
        {
            return (trimmed, null);
        }

        var key = trimmed[(hash + 1)..].Trim();
        if (key.Length == 0 || key.Contains('/'))
        {
            return (trimmed, null);
        }

        return (trimmed[..hash].Trim(), key);
    }

    private static (string Mount, string Path) SplitVaultPath(string vaultPath)
    {
        var trimmed = vaultPath.Trim().Trim('/');
        var slash = trimmed.IndexOf('/');
        if (slash < 0)
        {
            return ("secret", trimmed);
        }

        return (trimmed[..slash], trimmed[(slash + 1)..]);
    }

    private static (string User, string Password)? ExtractBasic(IDictionary<string, object> data)
    {
        var user = ExtractValue(data, null, UsernameKeys);
        var password = ExtractValue(data, null, PasswordKeys);
        return user is null || password is null ? null : (user, password);
    }

    private static (string User, string Password)? DecodePair(string? user, string? password, bool base64)
    {
        if (user is null || password is null)
        {
            return null;
        }

        if (!base64)
        {
            return (user, password);
        }

        return (DecodeBase64(user, "username"), DecodeBase64(password, "password"));
    }

    private static string DecodeBase64(string value, string name)
    {
        var compact = value.Trim().Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(compact));
            if (string.IsNullOrEmpty(decoded))
            {
                throw new InvalidOperationException($"Vault {name}: base64 декодировался в пустую строку.");
            }

            return decoded;
        }
        catch (FormatException)
        {
            throw new InvalidOperationException($"Vault {name} не является base64, а vault_base64: true.");
        }
    }

    private static string? ExtractValue(IDictionary<string, object> data, string? key, params string[] fallbacks)
    {
        var map = new Dictionary<string, object>(data, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(key) && map.TryGetValue(key, out var specified))
        {
            var fromKey = specified?.ToString();
            if (!string.IsNullOrWhiteSpace(fromKey))
            {
                return fromKey;
            }
        }

        foreach (var fallback in fallbacks)
        {
            if (map.TryGetValue(fallback, out var value))
            {
                var text = value?.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        var only = map.Values.Select(v => v?.ToString()).Where(v => !string.IsNullOrWhiteSpace(v)).Take(2).ToList();
        return only.Count == 1 ? only[0] : null;
    }

    private static List<EndpointEntity> ParseEndpoints(int serviceId, string module, OpenApiDocument document)
    {
        var list = new List<EndpointEntity>();
        foreach (var (path, item) in document.Paths)
        {
            if (item.Operations is null)
            {
                continue;
            }

            foreach (var (operationType, operation) in item.Operations)
            {
                var method = operationType.ToString().ToUpperInvariant();
                var jsonContent = OpenApiSchemaSupport.PickJsonContent(operation.RequestBody?.Content);

                OpenApiMediaType? responseMedia = null;
                if (operation.Responses is not null)
                {
                    var ok = operation.Responses.FirstOrDefault(r => r.Key.StartsWith('2'));
                    responseMedia = OpenApiSchemaSupport.PickJsonContent(ok.Value?.Content);
                }

                list.Add(new EndpointEntity
                {
                    ServiceId = serviceId,
                    Module = module,
                    Path = path,
                    Method = method,
                    Description = operation.Summary ?? operation.Description,
                    OperationId = operation.OperationId,
                    RequestSchema = ToJson(OpenApiSchemaSupport.Inline(document, jsonContent?.Schema)),
                    ResponseSchema = ToJson(OpenApiSchemaSupport.Inline(document, responseMedia?.Schema)),
                    Parameters = ToParametersJson(item, operation),
                    Tags = operation.Tags?.Select(t => t.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList() ?? []
                });
            }
        }

        return list;
    }

    private static JsonDocument? ToParametersJson(OpenApiPathItem pathItem, OpenApiOperation operation)
    {
        var merged = new List<OpenApiParameter>();
        if (pathItem.Parameters is { Count: > 0 })
        {
            merged.AddRange(pathItem.Parameters.Where(p => p is not null)!);
        }

        if (operation.Parameters is { Count: > 0 })
        {
            foreach (var parameter in operation.Parameters.Where(p => p is not null))
            {
                if (merged.Any(existing =>
                        string.Equals(existing.Name, parameter!.Name, StringComparison.OrdinalIgnoreCase)
                        && existing.In == parameter.In))
                {
                    continue;
                }

                merged.Add(parameter!);
            }
        }

        if (merged.Count == 0)
        {
            return null;
        }

        var rows = merged.Select(parameter =>
        {
            var row = new Dictionary<string, object?>
            {
                ["name"] = parameter.Name,
                ["in"] = parameter.In?.ToString().ToLowerInvariant() ?? "query",
                ["required"] = parameter.Required,
                ["description"] = parameter.Description,
                ["type"] = parameter.Schema?.Type,
                ["format"] = parameter.Schema?.Format
            };
            var enums = FormatEnumValues(parameter.Schema?.Enum);
            if (enums is { Count: > 0 })
            {
                row["enum"] = enums;
            }

            return row;
        }).ToList();

        return JsonDocument.Parse(JsonSerializer.Serialize(rows));
    }

    private static List<string>? FormatEnumValues(IList<Microsoft.OpenApi.Any.IOpenApiAny>? values)
    {
        if (values is null || values.Count == 0)
        {
            return null;
        }

        var list = new List<string>(values.Count);
        foreach (var value in values)
        {
            var text = FormatOpenApiAny(value);
            if (!string.IsNullOrWhiteSpace(text))
            {
                list.Add(text);
            }
        }

        return list.Count == 0 ? null : list;
    }

    private static string? FormatOpenApiAny(Microsoft.OpenApi.Any.IOpenApiAny? value) =>
        value switch
        {
            null => null,
            Microsoft.OpenApi.Any.OpenApiString s => s.Value,
            Microsoft.OpenApi.Any.OpenApiInteger i => i.Value.ToString(CultureInfo.InvariantCulture),
            Microsoft.OpenApi.Any.OpenApiLong l => l.Value.ToString(CultureInfo.InvariantCulture),
            Microsoft.OpenApi.Any.OpenApiFloat f => f.Value.ToString(CultureInfo.InvariantCulture),
            Microsoft.OpenApi.Any.OpenApiDouble d => d.Value.ToString(CultureInfo.InvariantCulture),
            Microsoft.OpenApi.Any.OpenApiBoolean b => b.Value ? "true" : "false",
            Microsoft.OpenApi.Any.OpenApiNull => "null",
            _ => value.ToString()
        };

    /// <summary>
    /// Prefer original JSON body for stable contract diffs; convert OpenAPI YAML to JSON.
    /// </summary>
    private static JsonDocument ToJsonDocument(string text, OpenApiDocument document)
    {
        var trimmed = text.AsSpan().TrimStart();
        if (trimmed.Length > 0 && trimmed[0] == '{')
        {
            try
            {
                return JsonDocument.Parse(text);
            }
            catch (JsonException)
            {
                // Fall through and serialize the parsed OpenAPI model.
            }
        }

        var asJson = document.SerializeAsJson(OpenApiSpecVersion.OpenApi3_0);
        if (string.IsNullOrWhiteSpace(asJson))
        {
            throw new InvalidOperationException("Не удалось сериализовать OpenAPI (YAML/JSON) в JSON.");
        }

        return JsonDocument.Parse(asJson);
    }

    private static JsonDocument? ToJson(OpenApiSchema? schema)
    {
        if (schema is null)
        {
            return null;
        }

        var json = schema.SerializeAsJson(OpenApiSpecVersion.OpenApi3_0);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var parsed = JsonDocument.Parse(json);
        var limited = OpenApiSchemaSupport.LimitDepth(parsed.RootElement);
        return limited is null
            ? null
            : JsonDocument.Parse(limited.Value.GetRawText());
    }

    private static bool IsLocalSource(string source) =>
        !source.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        && !source.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    private static SwaggerServiceResult Fail(string name, string error) =>
        new() { Service = name, Status = "error", Error = error };

    private static string Truncate(string value) =>
        value.Length <= 180 ? value : value[..180] + "…";
}
