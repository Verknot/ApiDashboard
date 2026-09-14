using System.Text;
using System.Text.Json;
using ApiWorkbench.Api.Configuration;
using ApiWorkbench.Api.Data;
using ApiWorkbench.Api.Domain;
using Microsoft.EntityFrameworkCore;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ApiWorkbench.Api.Services;

public sealed class CatalogReloadResult
{
    public int Upserted { get; init; }
    public int Deactivated { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed class ConfigFile
{
    public required string Path { get; init; }
    public required bool Writable { get; init; }
    public required string Content { get; init; }
}

public interface ICatalogSyncService
{
    Task<CatalogReloadResult> ReloadFromYamlAsync(CancellationToken cancellationToken = default);
    Task<ConfigFile> GetYamlAsync(CancellationToken cancellationToken = default);
    Task<CatalogReloadResult> SaveYamlAsync(string content, CancellationToken cancellationToken = default);
}

public sealed class CatalogSyncService(
    AppDbContext db,
    IConfiguration configuration,
    IHostEnvironment hostEnvironment,
    ILogger<CatalogSyncService> logger) : ICatalogSyncService
{
    private static readonly IDeserializer Yaml = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .WithTypeConverter(new SwaggerYamlListConverter())
        .WithTypeConverter(new StringOrEnvMapConverter())
        .IgnoreUnmatchedProperties()
        .Build();

    public async Task<CatalogReloadResult> ReloadFromYamlAsync(CancellationToken cancellationToken = default)
    {
        var path = ResolveConfigPath();
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Не найден services.yaml: {path}");
        }

        var yaml = await File.ReadAllTextAsync(path, new UTF8Encoding(false), cancellationToken);
        var file = Yaml.Deserialize<ServicesYamlFile>(yaml) ?? new ServicesYamlFile();
        var warnings = new List<string>();
        var namesInConfig = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var upserted = 0;

        foreach (var entry in file.Services)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                warnings.Add("Пропущен сервис без имени.");
                continue;
            }

            try
            {
                var resolved = ResolveService(entry, file.SplunkUrl);
                await UpsertServiceAsync(resolved, cancellationToken);
                namesInConfig.Add(entry.Name);
                upserted++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Не удалось загрузить сервис {Service}", entry.Name);
                warnings.Add($"{entry.Name}: {ex.Message}");
            }
        }

        var now = DateTimeOffset.UtcNow;
        var stale = await db.Services
            .Where(s => s.IsActive && !namesInConfig.Contains(s.Name))
            .ToListAsync(cancellationToken);

        foreach (var service in stale)
        {
            service.IsActive = false;
            service.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);

        return new CatalogReloadResult
        {
            Upserted = upserted,
            Deactivated = stale.Count,
            Warnings = warnings
        };
    }

    public async Task<ConfigFile> GetYamlAsync(CancellationToken cancellationToken = default)
    {
        var path = ResolveConfigPath();
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Не найден services.yaml: {path}");
        }

        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var content = await File.ReadAllTextAsync(path, utf8, cancellationToken);
        return new ConfigFile
        {
            Path = path,
            Writable = IsWritable(path),
            Content = content
        };
    }

    public async Task<CatalogReloadResult> SaveYamlAsync(string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Конфиг пустой.");
        }

        try
        {
            _ = Yaml.Deserialize<ServicesYamlFile>(content) ?? throw new InvalidOperationException("Не удалось разобрать YAML.");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Некорректный YAML: {ex.Message}");
        }

        var path = ResolveConfigPath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(path) && !IsWritable(path))
        {
            throw new UnauthorizedAccessException(
                "Файл конфигурации только для чтения (например, ConfigMap). Измените YAML снаружи и нажмите «Перечитать».");
        }

        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var tempPath = path + ".tmp";
        await File.WriteAllTextAsync(tempPath, content.Replace("\r\n", "\n", StringComparison.Ordinal), utf8, cancellationToken);
        File.Move(tempPath, path, overwrite: true);

        return await ReloadFromYamlAsync(cancellationToken);
    }

    private static bool IsWritable(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private string ResolveConfigPath() => ServicesYamlLocator.Resolve(configuration, hostEnvironment);

    private static ResolvedService ResolveService(ServiceYamlEntry entry, string? defaultSplunkUrl)
    {
        var authType = (entry.Auth?.Type ?? "none").Trim().ToLowerInvariant();
        if (authType is not ("token" or "certificate" or "none"))
        {
            throw new InvalidOperationException($"Неизвестный auth.type '{entry.Auth?.Type}'.");
        }

        var regions = entry.Regions?
            .Where(r => !string.IsNullOrWhiteSpace(r.Code))
            .Select((r, i) => new ResolvedRegion(r.Code.Trim().ToLowerInvariant(), string.IsNullOrWhiteSpace(r.Label) ? r.Code.Trim().ToUpperInvariant() : r.Label.Trim(), i, r.Environments))
            .ToList() ?? [];

        var isRegional = regions.Count > 0;
        var swaggerSpecs = ResolveSwaggerSpecs(entry, regions, isRegional);
        var swagger = swaggerSpecs.FirstOrDefault();

        var urls = ResolveUrls(entry, regions, isRegional, entry.Environments, module: string.Empty, requireAll: true);
        foreach (var spec in swaggerSpecs.Where(item => !string.IsNullOrWhiteSpace(item.Name)))
        {
            urls.AddRange(ResolveUrls(entry, regions, isRegional, spec.Environments, spec.Name, requireAll: false));
        }

        var defaultRegion = entry.DefaultRegion?.Trim().ToLowerInvariant();
        if (isRegional)
        {
            if (string.IsNullOrWhiteSpace(defaultRegion) || regions.All(r => r.Code != defaultRegion))
            {
                defaultRegion = regions[0].Code;
            }
        }
        else
        {
            defaultRegion = null;
        }

        var tokenAuth = ResolveTokenAuth(entry.Auth, regions, isRegional, authType, module: string.Empty);
        var moduleTokenUrls = swaggerSpecs
            .SelectMany(spec => spec.Token.Urls)
            .ToList();
        if (moduleTokenUrls.Count > 0)
        {
            tokenAuth = tokenAuth with
            {
                Urls = tokenAuth.Urls.Concat(moduleTokenUrls).ToList()
            };
        }

        return new ResolvedService(
            entry.Name.Trim(),
            entry.Description?.Trim(),
            entry.Color?.Trim(),
            swagger?.Url,
            swagger?.AuthType ?? "none",
            swagger?.VaultPath,
            swagger?.VaultUsernamePath,
            swagger?.VaultPasswordPath,
            swagger?.VaultBase64 ?? false,
            swagger?.BasicUsername,
            swagger?.BasicPassword,
            authType,
            entry.Auth?.CertPath?.Trim(),
            FirstNonEmpty(entry.Auth?.CertBase64),
            FirstNonEmpty(entry.Auth?.CertVault),
            entry.Auth?.CertPassword,
            entry.Proxy ?? true,
            FirstNonEmpty(entry.SplunkUrl, defaultSplunkUrl),
            isRegional,
            defaultRegion,
            regions.Select(r => new ResolvedRegion(r.Code, r.Label, r.SortOrder, null)).ToList(),
            urls,
            swaggerSpecs,
            tokenAuth);
    }

    private static List<ResolvedSwagger> ResolveSwaggerSpecs(
        ServiceYamlEntry entry,
        IReadOnlyList<ResolvedRegion> regions,
        bool isRegional)
    {
        var items = entry.Swagger?.Items ?? [];
        if (items.Count == 0)
        {
            return [];
        }

        if (items.Count > 1 && items.Any(item => string.IsNullOrWhiteSpace(item.Name)))
        {
            throw new InvalidOperationException("Если swagger несколько, у каждого укажите name (UserPortal, backend, backOffice…).");
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ResolvedSwagger>();
        for (var i = 0; i < items.Count; i++)
        {
            var spec = items[i];
            var auth = (spec.Auth ?? "none").Trim().ToLowerInvariant();
            if (auth is not ("basic" or "none"))
            {
                throw new InvalidOperationException($"Неизвестный swagger.auth '{spec.Auth}'.");
            }

            var name = spec.Name?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(name) && !names.Add(name))
            {
                throw new InvalidOperationException($"Повторяется swagger.name '{name}'.");
            }

            var vaultUsername = FirstNonEmpty(spec.VaultUsername, spec.Vault?.Username);
            var vaultPassword = FirstNonEmpty(spec.VaultPassword, spec.Vault?.Password);
            var vaultPath = FirstNonEmpty(spec.VaultPath, spec.Vault?.Path);
            var vaultBase64 = spec.VaultBase64 || spec.Vault?.Base64 == true;
            var basicUsername = FirstNonEmpty(spec.Username);
            var basicPassword = FirstNonEmpty(spec.Password);
            if (auth == "basic")
            {
                var hasDirect = !string.IsNullOrWhiteSpace(basicUsername) || !string.IsNullOrWhiteSpace(basicPassword);
                if (hasDirect && (string.IsNullOrWhiteSpace(basicUsername) || string.IsNullOrWhiteSpace(basicPassword)))
                {
                    throw new InvalidOperationException("Для swagger basic укажите и username, и password.");
                }
            }

            if (spec.Environments is { Count: > 0 } && string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("environments у swagger укажите вместе с name.");
            }

            if (spec.ApiAuth is not null && string.IsNullOrWhiteSpace(name) && items.Count > 1)
            {
                throw new InvalidOperationException("api_auth у swagger укажите вместе с name.");
            }

            Dictionary<string, string>? swaggerEnvironments = null;
            if (spec.Environments is { Count: > 0 })
            {
                swaggerEnvironments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var (key, value) in spec.Environments)
                {
                    var env = key.Trim().ToLowerInvariant();
                    if (!ServiceEnvironments.All.Contains(env))
                    {
                        throw new InvalidOperationException($"Неизвестная среда '{key}' у swagger '{name}'.");
                    }

                    swaggerEnvironments[env] = value;
                }
            }

            string? apiAuthType = null;
            string? certPath = null;
            string? certBase64 = null;
            string? certVault = null;
            string? certPassword = null;
            var moduleToken = ResolvedTokenAuth.Empty;
            if (spec.ApiAuth is not null)
            {
                apiAuthType = (spec.ApiAuth.Type ?? "none").Trim().ToLowerInvariant();
                if (apiAuthType is not ("token" or "certificate" or "none"))
                {
                    throw new InvalidOperationException($"Неизвестный api_auth.type '{spec.ApiAuth.Type}' у swagger '{name}'.");
                }

                certPath = FirstNonEmpty(spec.ApiAuth.CertPath);
                certBase64 = FirstNonEmpty(spec.ApiAuth.CertBase64);
                certVault = FirstNonEmpty(spec.ApiAuth.CertVault);
                certPassword = spec.ApiAuth.CertPassword;
                moduleToken = ResolveTokenAuth(spec.ApiAuth, regions, isRegional, apiAuthType, name);
                if (apiAuthType == "token" && moduleToken.Urls.Count == 0)
                {
                    throw new InvalidOperationException($"У swagger '{name}' api_auth.type=token, но нет api_auth.token_url.");
                }
            }

            result.Add(new ResolvedSwagger(
                name,
                i,
                spec.Url?.Trim() ?? string.Empty,
                auth,
                vaultPath,
                vaultUsername,
                vaultPassword,
                vaultBase64,
                basicUsername,
                basicPassword,
                swaggerEnvironments,
                apiAuthType,
                certPath,
                certBase64,
                certVault,
                certPassword,
                moduleToken));
        }

        return result;
    }

    private static List<ResolvedUrl> ResolveUrls(
        ServiceYamlEntry entry,
        IReadOnlyList<ResolvedRegion> regions,
        bool isRegional,
        Dictionary<string, string>? environments,
        string module,
        bool requireAll)
    {
        var urls = new List<ResolvedUrl>();
        if (!isRegional)
        {
            foreach (var env in ServiceEnvironments.All)
            {
                if (environments is null || !environments.TryGetValue(env, out var url) || string.IsNullOrWhiteSpace(url))
                {
                    if (requireAll)
                    {
                        throw new InvalidOperationException($"Нет URL для среды '{env}'.");
                    }

                    continue;
                }

                urls.Add(new ResolvedUrl(env, string.Empty, url.Trim(), module));
            }

            return urls;
        }

        foreach (var region in regions)
        {
            foreach (var env in ServiceEnvironments.All)
            {
                if (requireAll)
                {
                    urls.Add(new ResolvedUrl(env, region.Code, ResolveRegionalUrl(entry, region, env), module));
                    continue;
                }

                if (environments is null || !environments.TryGetValue(env, out var template) || string.IsNullOrWhiteSpace(template))
                {
                    continue;
                }

                var url = template.Contains("{region}", StringComparison.OrdinalIgnoreCase)
                    ? template.Replace("{region}", region.Code, StringComparison.OrdinalIgnoreCase).Trim()
                    : template.Trim();
                urls.Add(new ResolvedUrl(env, region.Code, url, module));
            }
        }

        return urls;
    }

    private static ResolvedTokenAuth ResolveTokenAuth(
        AuthYaml? auth,
        IReadOnlyList<ResolvedRegion> regions,
        bool isRegional,
        string authType,
        string module)
    {
        if (authType != "token" || auth?.TokenUrl is null)
        {
            return ResolvedTokenAuth.Empty;
        }

        var map = auth.TokenUrl;
        foreach (var key in map.ByEnvironment.Keys)
        {
            if (!ServiceEnvironments.All.Contains(key.Trim().ToLowerInvariant()))
            {
                throw new InvalidOperationException($"Неизвестная среда '{key}' у auth.token_url.");
            }
        }

        var urls = new List<ResolvedTokenUrl>();
        IEnumerable<(string Env, string Region)> slots = isRegional
            ? regions.SelectMany(region => ServiceEnvironments.All.Select(env => (env, region.Code)))
            : ServiceEnvironments.All.Select(env => (env, string.Empty));

        foreach (var (env, region) in slots)
        {
            string? raw = null;
            if (map.ByEnvironment.TryGetValue(env, out var fromMap) && !string.IsNullOrWhiteSpace(fromMap))
            {
                raw = fromMap;
            }
            else if (!string.IsNullOrWhiteSpace(map.Scalar))
            {
                raw = map.Scalar;
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var url = raw
                .Replace("{environment}", env, StringComparison.OrdinalIgnoreCase)
                .Replace("{region}", region, StringComparison.OrdinalIgnoreCase)
                .Trim();
            urls.Add(new ResolvedTokenUrl(env, region, module, url));
        }

        string? bodyJson = null;
        if (auth.TokenBody is { Count: > 0 })
        {
            bodyJson = JsonSerializer.Serialize(auth.TokenBody);
        }

        var field = string.IsNullOrWhiteSpace(auth.TokenField) ? "accessToken" : auth.TokenField.Trim();
        return new ResolvedTokenAuth(
            urls,
            FirstNonEmpty(auth.Username),
            FirstNonEmpty(auth.Password),
            FirstNonEmpty(auth.VaultPath),
            FirstNonEmpty(auth.VaultUsername),
            FirstNonEmpty(auth.VaultPassword),
            auth.VaultBase64,
            bodyJson,
            field);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.Select(v => v?.Trim()).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static string ResolveRegionalUrl(ServiceYamlEntry entry, ResolvedRegion region, string env)
    {
        if (region.Environments is not null &&
            region.Environments.TryGetValue(env, out var explicitUrl) &&
            !string.IsNullOrWhiteSpace(explicitUrl))
        {
            return explicitUrl.Trim();
        }

        if (entry.Environments is not null &&
            entry.Environments.TryGetValue(env, out var template) &&
            !string.IsNullOrWhiteSpace(template))
        {
            if (!template.Contains("{region}", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Для региона '{region.Code}' нет URL среды '{env}' и шаблон не содержит {{region}}.");
            }

            return template.Replace("{region}", region.Code, StringComparison.OrdinalIgnoreCase).Trim();
        }

        throw new InvalidOperationException($"Не удалось собрать URL {entry.Name}/{region.Code}/{env}.");
    }

    private async Task UpsertServiceAsync(ResolvedService resolved, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var service = await db.Services
            .Include(s => s.Regions)
            .Include(s => s.Urls)
            .Include(s => s.SwaggerSources)
            .Include(s => s.TokenUrls)
            .FirstOrDefaultAsync(s => s.Name == resolved.Name, cancellationToken);

        if (service is null)
        {
            service = new ServiceEntity
            {
                Name = resolved.Name,
                CreatedAt = now
            };
            db.Services.Add(service);
        }

        service.Description = resolved.Description;
        service.Color = resolved.Color;
        service.SwaggerUrl = resolved.SwaggerUrl;
        service.SwaggerAuthType = resolved.SwaggerAuthType;
        service.SwaggerVaultPath = resolved.SwaggerVaultPath;
        service.SwaggerVaultUsernamePath = resolved.SwaggerVaultUsernamePath;
        service.SwaggerVaultPasswordPath = resolved.SwaggerVaultPasswordPath;
        service.SwaggerVaultBase64 = resolved.SwaggerVaultBase64;
        service.SwaggerBasicUsername = resolved.SwaggerBasicUsername;
        service.SwaggerBasicPassword = resolved.SwaggerBasicPassword;
        service.AuthType = resolved.AuthType;
        service.CertPath = resolved.CertPath;
        service.CertBase64 = resolved.CertBase64;
        service.CertVaultPath = resolved.CertVaultPath;
        service.CertPassword = resolved.CertPassword;
        service.TokenUsername = resolved.Token.Username;
        service.TokenPassword = resolved.Token.Password;
        service.TokenVaultPath = resolved.Token.VaultPath;
        service.TokenVaultUsernamePath = resolved.Token.VaultUsernamePath;
        service.TokenVaultPasswordPath = resolved.Token.VaultPasswordPath;
        service.TokenVaultBase64 = resolved.Token.VaultBase64;
        service.TokenBody = resolved.Token.BodyJson;
        service.TokenField = resolved.Token.TokenField;
        service.Proxy = resolved.Proxy;
        service.SplunkUrl = resolved.SplunkUrl;
        service.IsRegional = resolved.IsRegional;
        service.DefaultRegion = resolved.DefaultRegion;
        service.IsActive = true;
        service.UpdatedAt = now;

        db.ServiceRegions.RemoveRange(service.Regions);
        db.ServiceUrls.RemoveRange(service.Urls);
        db.ServiceSwaggerSources.RemoveRange(service.SwaggerSources);
        db.ServiceTokenUrls.RemoveRange(service.TokenUrls);

        foreach (var region in resolved.Regions)
        {
            service.Regions.Add(new ServiceRegion
            {
                Code = region.Code,
                Label = region.Label,
                SortOrder = region.SortOrder
            });
        }

        foreach (var url in resolved.Urls)
        {
            service.Urls.Add(new ServiceUrl
            {
                Environment = url.Environment,
                RegionCode = url.RegionCode,
                Module = url.Module,
                BaseUrl = url.BaseUrl
            });
        }

        foreach (var swagger in resolved.Swaggers)
        {
            service.SwaggerSources.Add(new ServiceSwaggerSource
            {
                Name = swagger.Name,
                SortOrder = swagger.SortOrder,
                Url = swagger.Url,
                AuthType = swagger.AuthType,
                VaultPath = swagger.VaultPath,
                VaultUsernamePath = swagger.VaultUsernamePath,
                VaultPasswordPath = swagger.VaultPasswordPath,
                VaultBase64 = swagger.VaultBase64,
                BasicUsername = swagger.BasicUsername,
                BasicPassword = swagger.BasicPassword,
                ApiAuthType = swagger.ApiAuthType,
                CertPath = swagger.CertPath,
                CertBase64 = swagger.CertBase64,
                CertVaultPath = swagger.CertVaultPath,
                CertPassword = swagger.CertPassword,
                TokenField = swagger.ApiAuthType is null ? null : swagger.Token.TokenField
            });
        }

        foreach (var tokenUrl in resolved.Token.Urls)
        {
            service.TokenUrls.Add(new ServiceTokenUrl
            {
                Environment = tokenUrl.Environment,
                RegionCode = tokenUrl.RegionCode,
                Module = tokenUrl.Module,
                Url = tokenUrl.Url
            });
        }
    }

    private sealed record ResolvedService(
        string Name,
        string? Description,
        string? Color,
        string? SwaggerUrl,
        string SwaggerAuthType,
        string? SwaggerVaultPath,
        string? SwaggerVaultUsernamePath,
        string? SwaggerVaultPasswordPath,
        bool SwaggerVaultBase64,
        string? SwaggerBasicUsername,
        string? SwaggerBasicPassword,
        string AuthType,
        string? CertPath,
        string? CertBase64,
        string? CertVaultPath,
        string? CertPassword,
        bool Proxy,
        string? SplunkUrl,
        bool IsRegional,
        string? DefaultRegion,
        IReadOnlyList<ResolvedRegion> Regions,
        IReadOnlyList<ResolvedUrl> Urls,
        IReadOnlyList<ResolvedSwagger> Swaggers,
        ResolvedTokenAuth Token);

    private sealed record ResolvedSwagger(
        string Name,
        int SortOrder,
        string Url,
        string AuthType,
        string? VaultPath,
        string? VaultUsernamePath,
        string? VaultPasswordPath,
        bool VaultBase64,
        string? BasicUsername,
        string? BasicPassword,
        Dictionary<string, string>? Environments,
        string? ApiAuthType,
        string? CertPath,
        string? CertBase64,
        string? CertVaultPath,
        string? CertPassword,
        ResolvedTokenAuth Token);

    private sealed record ResolvedRegion(
        string Code,
        string Label,
        int SortOrder,
        Dictionary<string, string>? Environments);

    private sealed record ResolvedUrl(string Environment, string RegionCode, string BaseUrl, string Module);

    private sealed record ResolvedTokenUrl(string Environment, string RegionCode, string Module, string Url);

    private sealed record ResolvedTokenAuth(
        IReadOnlyList<ResolvedTokenUrl> Urls,
        string? Username,
        string? Password,
        string? VaultPath,
        string? VaultUsernamePath,
        string? VaultPasswordPath,
        bool VaultBase64,
        string? BodyJson,
        string TokenField)
    {
        public static ResolvedTokenAuth Empty { get; } = new([], null, null, null, null, null, false, null, "accessToken");
    }
}
