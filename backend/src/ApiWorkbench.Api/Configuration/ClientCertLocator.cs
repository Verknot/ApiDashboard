using System.Security.Cryptography.X509Certificates;
using ApiWorkbench.Api.Domain;

namespace ApiWorkbench.Api.Configuration;

internal static class ClientCertLocator
{
    public const string WindowsHostDirectory = @"C:\pult-certs";
    public const string ContainerDirectory = "/certs";

    private static readonly string[] CertVaultKeys =
    [
        "certificate", "cert_base64", "pfx", "cert", "value"
    ];

    private static readonly string[] CertPasswordKeys =
    [
        "cert_password", "password", "pfx_password"
    ];

    public static bool NeedsClientCertificate(ServiceEntity service) =>
        string.Equals(service.AuthType, "certificate", StringComparison.OrdinalIgnoreCase)
        || !string.IsNullOrWhiteSpace(service.CertPath)
        || !string.IsNullOrWhiteSpace(service.CertBase64)
        || !string.IsNullOrWhiteSpace(service.CertVaultPath);

    public static HttpClientHandler CreateHandler(
        ServiceEntity service,
        IConfiguration configuration,
        IHostEnvironment? environment)
    {
        var certificate = LoadCertificate(service, configuration, environment);
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            ClientCertificateOptions = ClientCertificateOption.Manual
        };
        handler.ClientCertificates.Add(certificate);
        return handler;
    }

    public static X509Certificate2 LoadCertificate(
        ServiceEntity service,
        IConfiguration configuration,
        IHostEnvironment? environment)
    {
        var password = service.CertPassword ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(service.CertBase64))
        {
            return LoadFromBase64(service.CertBase64, password);
        }

        if (!string.IsNullOrWhiteSpace(service.CertVaultPath))
        {
            var raw = VaultKvReader.ReadString(configuration, service.CertVaultPath, CertVaultKeys);
            if (string.IsNullOrWhiteSpace(password))
            {
                try
                {
                    password = VaultKvReader.ReadString(configuration, service.CertVaultPath, CertPasswordKeys);
                }
                catch (InvalidOperationException)
                {
                    password = string.Empty;
                }
            }

            return LoadFromBase64(raw, password);
        }

        var path = ResolveFile(service.CertPath, configuration, environment);
        if (path is null)
        {
            throw new InvalidOperationException(MissingFileMessage(service));
        }

        return LoadFromFile(path, password);
    }

    public static X509Certificate2 LoadFromFile(string path, string? password)
    {
        var pwd = password ?? string.Empty;
        try
        {
            return X509CertificateLoader.LoadPkcs12FromFile(
                path,
                pwd,
                X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.MachineKeySet);
        }
        catch (Exception)
        {
            return X509CertificateLoader.LoadPkcs12FromFile(path, pwd, X509KeyStorageFlags.UserKeySet);
        }
    }

    public static X509Certificate2 LoadFromBase64(string base64, string? password)
    {
        var bytes = DecodePfxBytes(base64);
        var pwd = password ?? string.Empty;
        try
        {
            return X509CertificateLoader.LoadPkcs12(
                bytes,
                pwd,
                X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.MachineKeySet);
        }
        catch (Exception)
        {
            return X509CertificateLoader.LoadPkcs12(bytes, pwd, X509KeyStorageFlags.UserKeySet);
        }
    }

    public static byte[] DecodePfxBytes(string base64)
    {
        var compact = base64.Trim()
            .Replace("\r", "", StringComparison.Ordinal)
            .Replace("\n", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal);
        if (compact.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = compact.IndexOf(',');
            if (comma > 0)
            {
                compact = compact[(comma + 1)..];
            }
        }

        try
        {
            var bytes = Convert.FromBase64String(compact);
            if (bytes.Length == 0)
            {
                throw new InvalidOperationException("cert_base64 decoded to empty bytes.");
            }

            return bytes;
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("cert_base64 is not valid Base64.", ex);
        }
    }

    public static string RepoCertsDirectory(IConfiguration configuration, IHostEnvironment? environment)
    {
        if (Path.Exists(WindowsHostDirectory))
        {
            return WindowsHostDirectory;
        }

        if (Directory.Exists(ContainerDirectory))
        {
            return ContainerDirectory;
        }

        var yaml = ServicesYamlLocator.Resolve(configuration, environment);
        var yamlDir = Path.GetDirectoryName(yaml);
        var repoRoot = yamlDir is null ? Directory.GetCurrentDirectory() : (Path.GetDirectoryName(yamlDir) ?? yamlDir);
        return Path.GetFullPath(Path.Combine(repoRoot, "certs"));
    }

    public static string? ResolveFile(string? certPath, IConfiguration configuration, IHostEnvironment? environment)
    {
        if (string.IsNullOrWhiteSpace(certPath))
        {
            return null;
        }

        var yaml = ServicesYamlLocator.Resolve(configuration, environment);
        var yamlDir = Path.GetDirectoryName(yaml) ?? Directory.GetCurrentDirectory();
        var repoRoot = Path.GetDirectoryName(yamlDir) ?? yamlDir;
        var fileName = Path.GetFileName(certPath.Replace('\\', '/'));

        var candidates = new List<string>
        {
            Path.Combine(ContainerDirectory, fileName),
            Path.Combine(WindowsHostDirectory, fileName),
            Path.GetFullPath(certPath),
            Path.GetFullPath(Path.Combine(yamlDir, certPath)),
            Path.GetFullPath(Path.Combine(repoRoot, certPath.TrimStart('/'))),
            Path.GetFullPath(Path.Combine(repoRoot, "certs", fileName))
        };

        return candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(File.Exists);
    }

    public static string MissingFileMessage(ServiceEntity service)
    {
        if (!string.IsNullOrWhiteSpace(service.CertVaultPath))
        {
            return $"No PFX for {service.Name}: Vault path '{service.CertVaultPath}'.";
        }

        if (!string.IsNullOrWhiteSpace(service.CertBase64))
        {
            return $"No PFX for {service.Name}: cert_base64 is invalid.";
        }

        var name = string.IsNullOrWhiteSpace(service.CertPath)
            ? "client.pfx"
            : Path.GetFileName(service.CertPath.Replace('\\', '/'));
        return $"No PFX for {service.Name}. Put {name} in {WindowsHostDirectory}, set auth.cert_base64, or auth.cert_vault.";
    }
}
