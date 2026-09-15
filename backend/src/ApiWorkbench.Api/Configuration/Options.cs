namespace ApiWorkbench.Api.Configuration;

public sealed class ServicesYamlFile
{
    public string? SplunkUrl { get; set; }
    public List<ServiceYamlEntry> Services { get; set; } = [];
}

public sealed class ServiceYamlEntry
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public bool? Proxy { get; set; }
    public string? SplunkUrl { get; set; }
    public string? DefaultRegion { get; set; }
    public List<PortalYaml>? Portals { get; set; }

    /// <summary>Region codes, e.g. [eu, tr, br, mx].</summary>
    public List<string>? Regions { get; set; }
}
public sealed class PortalYaml
{
    public string Name { get; set; } = string.Empty;
    public SwaggerDownloadYaml? Swagger { get; set; }

    /// <summary>Per-env base URLs. With regions, use {region} (and optional global_urls for non-geo host).</summary>
    public Dictionary<string, string>? Urls { get; set; }

    /// <summary>Optional non-regional bases alongside regional urls (empty regionCode).</summary>
    public Dictionary<string, string>? GlobalUrls { get; set; }

    public AuthYaml? Auth { get; set; }
}

public sealed class SwaggerDownloadYaml
{
    public string? Url { get; set; }
    public SwaggerBasicYaml? Basic { get; set; }
}

public sealed class SwaggerBasicYaml
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? VaultUsername { get; set; }
    public string? VaultPassword { get; set; }
    public string? VaultPath { get; set; }
    public bool VaultBase64 { get; set; }
}

public sealed class AuthYaml
{
    public string Type { get; set; } = "none";

    /// <summary>Short alias for cert_path (file under C:\pult-certs).</summary>
    public string? Cert { get; set; }
    public string? CertPath { get; set; }
    public string? CertBase64 { get; set; }
    public string? CertVault { get; set; }
    public string? CertPassword { get; set; }
    public StringOrEnvMap? TokenUrl { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? VaultPath { get; set; }
    public string? VaultUsername { get; set; }
    public string? VaultPassword { get; set; }
    public bool VaultBase64 { get; set; }
    public Dictionary<string, string>? TokenBody { get; set; }
    public string? TokenField { get; set; }

    public string? ResolvedCertPath =>
        string.IsNullOrWhiteSpace(CertPath) ? Cert?.Trim() : CertPath.Trim();
}

public sealed class RegionYaml
{
    public string Code { get; set; } = string.Empty;
    public string? Label { get; set; }
    public Dictionary<string, string>? Environments { get; set; }
}

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "ApiWorkbench";
    public string Audience { get; set; } = "ApiWorkbench";
    public string Key { get; set; } = string.Empty;
    public int ExpirationHours { get; set; } = 8;
}

public sealed class HistoryOptions
{
    public const string SectionName = "History";

    public int MaxPerUser { get; set; } = 100;
    public int MaxResponseBodyBytes { get; set; } = 262144;
}

public sealed class VaultOptions
{
    public const string SectionName = "Vault";

    public string Address { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;

    public string? ConnectionStringPath { get; set; }
    public string? JwtKeyPath { get; set; }
    public string? SeedAdminPasswordPath { get; set; }
}

public static class AuthCookieNames
{
    public const string AccessToken = "access_token";
}

public static class ServiceEnvironments
{
    public static readonly string[] PreferredOrder = ["dev", "qa", "stage", "uat", "prod", "production"];

    public static string Normalize(string? name)
    {
        var env = (name ?? string.Empty).Trim().ToLowerInvariant();
        if (env.Length is < 1 or > 20 || !env.All(static c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
        {
            throw new InvalidOperationException(
                $"Некорректное имя среды '{name}'. Допустимы a-z, 0-9, -, _ (1–20 символов).");
        }

        return env;
    }

    public static bool IsProductionLike(string? name)
    {
        var env = (name ?? string.Empty).Trim().ToLowerInvariant();
        return env is "prod" or "production" or "live";
    }

    public static IReadOnlyList<string> Order(IEnumerable<string> names) =>
        names
            .Select(Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name =>
            {
                var idx = Array.FindIndex(PreferredOrder, item => item.Equals(name, StringComparison.OrdinalIgnoreCase));
                return idx < 0 ? 1000 : idx;
            })
            .ThenBy(name => name, StringComparer.Ordinal)
            .ToList();
}
