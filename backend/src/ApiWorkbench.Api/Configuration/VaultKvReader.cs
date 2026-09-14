using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;

namespace ApiWorkbench.Api.Configuration;

/// <summary>Minimal Vault KV reader for startup and client certificates.</summary>
internal static class VaultKvReader
{
    public static async Task<string> ReadStringAsync(
        IConfiguration configuration,
        string reference,
        string[] fallbackKeys,
        CancellationToken cancellationToken = default)
    {
        var options = configuration.GetSection(VaultOptions.SectionName).Get<VaultOptions>()
                      ?? throw new InvalidOperationException("Vault section is not configured.");
        if (string.IsNullOrWhiteSpace(options.Address) || string.IsNullOrWhiteSpace(options.Token))
        {
            throw new InvalidOperationException("Vault:Address and Vault:Token are required.");
        }

        var (path, key) = SplitSecretRef(reference);
        var client = new VaultClient(new VaultClientSettings(options.Address, new TokenAuthMethodInfo(options.Token)));
        var data = await ReadDataAsync(client, path, cancellationToken)
                   ?? throw new InvalidOperationException($"Vault secret '{path}' not found.");
        var value = ExtractValue(data, key, fallbackKeys);
        if (string.IsNullOrWhiteSpace(value))
        {
            var hint = key is null ? string.Join(", ", fallbackKeys) : key;
            throw new InvalidOperationException($"Vault secret '{path}' has no value for key(s): {hint}.");
        }

        return value.Trim();
    }

    public static string ReadString(IConfiguration configuration, string reference, string[] fallbackKeys) =>
        ReadStringAsync(configuration, reference, fallbackKeys).GetAwaiter().GetResult();

    private static async Task<IDictionary<string, object>?> ReadDataAsync(
        VaultClient client,
        string vaultPath,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var (mount, path) = SplitVaultPath(vaultPath);
        try
        {
            var secret = await client.V1.Secrets.KeyValue.V2.ReadSecretAsync(path: path, mountPoint: mount);
            return secret.Data.Data;
        }
        catch (Exception)
        {
            try
            {
                var secret = await client.V1.Secrets.KeyValue.V1.ReadSecretAsync(path: path, mountPoint: mount);
                return secret.Data;
            }
            catch
            {
                return null;
            }
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

    private static string? ExtractValue(IDictionary<string, object> data, string? key, string[] fallbacks)
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
}

internal static class ConnectionStringResolver
{
    private static readonly string[] ConnectionStringKeys =
    [
        "connectionString", "connection_string", "ConnectionString", "value", "default"
    ];

    public static async Task<string> ResolveAsync(IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var configured = configuration.GetConnectionString("Default");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        var vaultPath = configuration["Vault:ConnectionStringPath"]?.Trim();
        if (string.IsNullOrWhiteSpace(vaultPath))
        {
            throw new InvalidOperationException(
                "Set ConnectionStrings:Default or Vault:ConnectionStringPath (e.g. secret/pult/db#connectionString).");
        }

        return await VaultKvReader.ReadStringAsync(configuration, vaultPath, ConnectionStringKeys, cancellationToken);
    }
}
