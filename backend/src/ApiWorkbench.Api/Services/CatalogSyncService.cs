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
        var portals = entry.Portals ?? [];
        if (portals.Count == 0)
        {
            throw new InvalidOperationException($"У сервиса '{entry.Name}' укажите portals (UserPortal, BackOffice, …).");
        }

        var regions = (entry.Regions ?? [])
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select((code, i) =>
            {
                var normalized = code.Trim().ToLowerInvariant();
                return new ResolvedRegion(normalized, normalized.ToUpperInvariant(), i);
            })
            .ToList();

        var isRegional = regions.Count > 0;
        var declaredEnvs = CollectDeclaredEnvironments(portals);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var swaggerSpecs = new List<ResolvedSwagger>();
        var urls = new List<ResolvedUrl>();
        var tokenUrls = new List<ResolvedTokenUrl>();
        ResolvedTokenAuth? sharedTokenMeta = null;

        for (var i = 0; i < portals.Count; i++)
        {
            var portal = portals[i];
            var name = portal.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException($"У сервиса '{entry.Name}' portal #{i + 1} без name.");
            }

            if (!names.Add(name))
            {
                throw new InvalidOperationException($"Повторяется portal.name '{name}'.");
            }

            var swaggerUrl = portal.Swagger?.Url?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(swaggerUrl))
            {
                throw new InvalidOperationException($"У portal '{name}' укажите swagger.url.");
            }

            var basic = portal.Swagger?.Basic;
            string swaggerAuthType = "none";
            string? basicUsername = null;
            string? basicPassword = null;
            string? vaultUsername = null;
            string? vaultPassword = null;
            string? vaultPath = null;
            var vaultBase64 = false;
            if (basic is not null)
            {
                swaggerAuthType = "basic";
                basicUsername = FirstNonEmpty(basic.Username);
                basicPassword = FirstNonEmpty(basic.Password);
                vaultUsername = FirstNonEmpty(basic.VaultUsername);
                vaultPassword = FirstNonEmpty(basic.VaultPassword);
                vaultPath = FirstNonEmpty(basic.VaultPath);
                vaultBase64 = basic.VaultBase64;
                var hasDirect = !string.IsNullOrWhiteSpace(basicUsername) || !string.IsNullOrWhiteSpace(basicPassword);
                if (hasDirect && (string.IsNullOrWhiteSpace(basicUsername) || string.IsNullOrWhiteSpace(basicPassword)))
                {
                    throw new InvalidOperationException($"У portal '{name}' swagger.basic: укажите и username, и password.");
                }
            }

            var urlsMap = portal.Urls ?? new Dictionary<string, string>();
            if (urlsMap.Count == 0 && (portal.GlobalUrls is null || portal.GlobalUrls.Count == 0))
            {
                throw new InvalidOperationException($"У portal '{name}' укажите urls (хотя бы одну среду).");
            }

            var normalizedUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in urlsMap)
            {
                normalizedUrls[ServiceEnvironments.Normalize(key)] = value;
            }

            var normalizedGlobalUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (portal.GlobalUrls is not null)
            {
                foreach (var (key, value) in portal.GlobalUrls)
                {
                    normalizedGlobalUrls[ServiceEnvironments.Normalize(key)] = value;
                }
            }

            urls.AddRange(ResolvePortalUrls(regions, isRegional, normalizedUrls, normalizedGlobalUrls, name));

            var auth = portal.Auth ?? new AuthYaml { Type = "none" };
            var apiAuthType = (auth.Type ?? "none").Trim().ToLowerInvariant();
            if (apiAuthType is not ("token" or "certificate" or "none"))
            {
                throw new InvalidOperationException($"Неизвестный auth.type '{auth.Type}' у portal '{name}'.");
            }

            var certPath = auth.ResolvedCertPath;
            var certBase64 = FirstNonEmpty(auth.CertBase64);
            var certVault = FirstNonEmpty(auth.CertVault);
            var certPassword = auth.CertPassword;
            var hasGlobal = normalizedGlobalUrls.Count > 0
                            || normalizedUrls.Values.Any(v =>
                                !string.IsNullOrWhiteSpace(v)
                                && !v.Contains("{region}", StringComparison.OrdinalIgnoreCase));
            var moduleToken = ResolveTokenAuth(auth, regions, isRegional, hasGlobal, apiAuthType, name, declaredEnvs);
            if (apiAuthType == "token" && moduleToken.Urls.Count == 0)
            {
                throw new InvalidOperationException($"У portal '{name}' auth.type=token, но нет auth.token_url.");
            }

            if (apiAuthType is "token" or "certificate"
                && string.IsNullOrWhiteSpace(certPath)
                && string.IsNullOrWhiteSpace(certBase64)
                && string.IsNullOrWhiteSpace(certVault)
                && apiAuthType == "certificate")
            {
                throw new InvalidOperationException(
                    $"У portal '{name}' auth.type=certificate: укажите auth.cert / cert_base64 / cert_vault.");
            }

            tokenUrls.AddRange(moduleToken.Urls);
            sharedTokenMeta ??= moduleToken;

            swaggerSpecs.Add(new ResolvedSwagger(
                name,
                i,
                swaggerUrl,
                swaggerAuthType,
                vaultPath,
                vaultUsername,
                vaultPassword,
                vaultBase64,
                basicUsername,
                basicPassword,
                normalizedUrls,
                apiAuthType,
                certPath,
                certBase64,
                certVault,
                certPassword,
                moduleToken));
        }

        var defaultRegion = entry.DefaultRegion?.Trim().ToLowerInvariant();
        if (isRegional)
        {
            if (IsGlobalRegionAlias(defaultRegion))
            {
                defaultRegion = string.Empty;
            }
            else if (string.IsNullOrWhiteSpace(defaultRegion) || regions.All(r => r.Code != defaultRegion))
            {
                defaultRegion = regions[0].Code;
            }
        }
        else
        {
            defaultRegion = null;
        }

        var first = swaggerSpecs[0];
        var tokenAuth = (sharedTokenMeta ?? ResolvedTokenAuth.Empty) with { Urls = tokenUrls };

        return new ResolvedService(
            entry.Name.Trim(),
            entry.Description?.Trim(),
            entry.Color?.Trim(),
            first.Url,
            first.AuthType,
            first.VaultPath,
            first.VaultUsernamePath,
            first.VaultPasswordPath,
            first.VaultBase64,
            first.BasicUsername,
            first.BasicPassword,
            "none",
            null,
            null,
            null,
            null,
            entry.Proxy ?? true,
            FirstNonEmpty(entry.SplunkUrl, defaultSplunkUrl),
            isRegional,
            defaultRegion,
            regions.Select(r => new ResolvedRegion(r.Code, r.Label, r.SortOrder)).ToList(),
            urls,
            swaggerSpecs,
            tokenAuth);
    }

    private static IReadOnlyList<string> CollectDeclaredEnvironments(IReadOnlyList<PortalYaml> portals)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var portal in portals)
        {
            if (portal.Urls is not null)
            {
                foreach (var key in portal.Urls.Keys)
                {
                    keys.Add(ServiceEnvironments.Normalize(key));
                }
            }

            if (portal.GlobalUrls is not null)
            {
                foreach (var key in portal.GlobalUrls.Keys)
                {
                    keys.Add(ServiceEnvironments.Normalize(key));
                }
            }

            if (portal.Auth?.TokenUrl is { } tokenUrl)
            {
                foreach (var key in tokenUrl.ByEnvironment.Keys)
                {
                    keys.Add(ServiceEnvironments.Normalize(key));
                }
            }
        }

        if (keys.Count == 0)
        {
            throw new InvalidOperationException("Нет urls у portals. Укажите хотя бы одну среду (dev / qa / …).");
        }

        return ServiceEnvironments.Order(keys);
    }

    private static List<ResolvedUrl> ResolvePortalUrls(
        IReadOnlyList<ResolvedRegion> regions,
        bool isRegional,
        Dictionary<string, string> urlsMap,
        Dictionary<string, string> globalUrlsMap,
        string module)
    {
        var urls = new List<ResolvedUrl>();
        var envKeys = ServiceEnvironments.Order(urlsMap.Keys.Concat(globalUrlsMap.Keys));

        if (!isRegional)
        {
            foreach (var env in envKeys)
            {
                var url = LookupEnv(urlsMap, env) ?? LookupEnv(globalUrlsMap, env);
                if (string.IsNullOrWhiteSpace(url))
                {
                    throw new InvalidOperationException($"У portal '{module}' нет URL для среды '{env}'.");
                }

                if (url.Contains("{region}", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"У portal '{module}' URL среды '{env}' содержит {{region}}, но у сервиса нет regions.");
                }

                urls.Add(new ResolvedUrl(env, string.Empty, url.Trim(), module));
            }

            return urls;
        }

        foreach (var env in envKeys)
        {
            var template = LookupEnv(urlsMap, env);
            var global = LookupEnv(globalUrlsMap, env);

            if (!string.IsNullOrWhiteSpace(template)
                && template.Contains("{region}", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var region in regions)
                {
                    var url = template.Replace("{region}", region.Code, StringComparison.OrdinalIgnoreCase).Trim();
                    urls.Add(new ResolvedUrl(env, region.Code, url, module));
                }
            }
            else if (!string.IsNullOrWhiteSpace(template))
            {
                // urls without {region} on a regional service → non-geo ("global") host
                urls.Add(new ResolvedUrl(env, string.Empty, template.Trim(), module));
            }

            if (!string.IsNullOrWhiteSpace(global))
            {
                if (global.Contains("{region}", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"У portal '{module}' global_urls среды '{env}' не должен содержать {{region}}.");
                }

                if (!urls.Any(u => u.Environment == env && u.RegionCode == string.Empty && u.Module == module))
                {
                    urls.Add(new ResolvedUrl(env, string.Empty, global.Trim(), module));
                }
            }

            if (!urls.Any(u => u.Environment == env && u.Module == module))
            {
                throw new InvalidOperationException($"У portal '{module}' нет URL для среды '{env}'.");
            }
        }

        return urls;
    }

    private static bool IsGlobalRegionAlias(string? code) =>
        code is "global" or "none" or "no" or "-" or "_";

    private static string? LookupEnv(Dictionary<string, string>? environments, string env)
    {
        if (environments is null || environments.Count == 0)
        {
            return null;
        }

        if (environments.TryGetValue(env, out var direct) && !string.IsNullOrWhiteSpace(direct))
        {
            return direct;
        }

        return environments.FirstOrDefault(kv => ServiceEnvironments.Normalize(kv.Key) == env).Value;
    }

    private static ResolvedTokenAuth ResolveTokenAuth(
        AuthYaml? auth,
        IReadOnlyList<ResolvedRegion> regions,
        bool isRegional,
        bool includeGlobalSlot,
        string authType,
        string module,
        IReadOnlyList<string> environments)
    {
        if (authType != "token" || auth?.TokenUrl is null)
        {
            return ResolvedTokenAuth.Empty;
        }

        var map = auth.TokenUrl;
        var envKeys = map.ByEnvironment.Count > 0
            ? ServiceEnvironments.Order(map.ByEnvironment.Keys)
            : environments;

        var urls = new List<ResolvedTokenUrl>();
        IEnumerable<(string Env, string Region)> slots = isRegional
            ? regions.SelectMany(region => envKeys.Select(env => (env, region.Code)))
                .Concat(includeGlobalSlot ? envKeys.Select(env => (env, string.Empty)) : [])
            : envKeys.Select(env => (env, string.Empty));

        foreach (var (env, region) in slots)
        {
            string? raw = null;
            if (map.ByEnvironment.TryGetValue(env, out var fromMap) && !string.IsNullOrWhiteSpace(fromMap))
            {
                raw = fromMap;
            }
            else
            {
                var matched = map.ByEnvironment.FirstOrDefault(kv =>
                    ServiceEnvironments.Normalize(kv.Key) == env);
                if (!string.IsNullOrWhiteSpace(matched.Value))
                {
                    raw = matched.Value;
                }
                else if (!string.IsNullOrWhiteSpace(map.Scalar))
                {
                    raw = map.Scalar;
                }
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            if (string.IsNullOrEmpty(region)
                && raw.Contains("{region}", StringComparison.OrdinalIgnoreCase))
            {
                // global host has no geo — skip geo-bound token templates
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

    private sealed record ResolvedRegion(string Code, string Label, int SortOrder);

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
