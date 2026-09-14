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
    public SwaggerYamlList? Swagger { get; set; }
    public Dictionary<string, string>? Environments { get; set; }
    public List<RegionYaml>? Regions { get; set; }
    public AuthYaml? Auth { get; set; }
}

public sealed class SwaggerYaml
{
    public string Auth { get; set; } = "none";
    public string? Name { get; set; }
    public string? Url { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? VaultPath { get; set; }
    public string? VaultUsername { get; set; }
    public string? VaultPassword { get; set; }
    public bool VaultBase64 { get; set; }
    public Dictionary<string, string>? Environments { get; set; }
    public SwaggerVaultYaml? Vault { get; set; }
}

public sealed class SwaggerVaultYaml
{
    public string? Path { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool Base64 { get; set; }
}

public sealed class AuthYaml
{
    public string Type { get; set; } = "none";
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
}

public sealed class RegionYaml
{
    public string Code { get; set; } = string.Empty;
    public string? Label { get; set; }
    public Dictionary<string, string>? Environments { get; set; }
    public string? SwaggerUrl { get; set; }
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

    /// <summary>
    /// Optional Vault KV path for Postgres connection string when ConnectionStrings:Default is empty.
    /// Example: secret/pult/db#connectionString
    /// </summary>
    public string? ConnectionStringPath { get; set; }
}

public static class AuthCookieNames
{
    public const string AccessToken = "access_token";
}

public static class ServiceEnvironments
{
    public static readonly string[] All = ["dev", "stage", "prod"];
}
