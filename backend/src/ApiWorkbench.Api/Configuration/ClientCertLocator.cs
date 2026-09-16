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
        NeedsClientCertificate(ServiceAuthResolver.Resolve(service));

    public static bool NeedsClientCertificate(ServiceAuthContext auth) => auth.NeedsClientCertificate;

    public static HttpClientHandler CreateHandler(
        ServiceEntity service,
        IConfiguration configuration,
        IHostEnvironment? environment) =>
        CreateHandler(ServiceAuthResolver.Resolve(service), service.Name, configuration, environment);

    public static HttpClientHandler CreateHandler(
        ServiceAuthContext auth,
        string serviceName,
        IConfiguration configuration,
        IHostEnvironment? environment)
    {
        var certificate = LoadCertificate(auth, serviceName, configuration, environment);
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            ClientCertificateOptions = ClientCertificateOption.Manual
        };
        handler.ClientCertificates.Add(certificate);
        return handler;
    }

    public static void AllowInsecureServerCertificate(HttpClientHandler handler)
    {
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }

    public static HttpClientHandler CreateInsecureRelayHandler()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false
        };
        AllowInsecureServerCertificate(handler);
        return handler;
    }

    public static X509Certificate2 LoadCertificate(
        ServiceEntity service,
        IConfiguration configuration,
        IHostEnvironment? environment) =>
        LoadCertificate(ServiceAuthResolver.Resolve(service), service.Name, configuration, environment);

    public static X509Certificate2 LoadCertificate(
        ServiceAuthContext auth,
        string serviceName,
        IConfiguration configuration,
        IHostEnvironment? environment)
    {
        var password = auth.CertPassword ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(auth.CertBase64))
        {
            return LoadFromBase64(auth.CertBase64, password);
        }

        if (!string.IsNullOrWhiteSpace(auth.CertVaultPath))
        {
            var raw = VaultKvReader.ReadString(configuration, auth.CertVaultPath, CertVaultKeys);
            if (string.IsNullOrWhiteSpace(password))
            {
                try
                {
                    password = VaultKvReader.ReadString(configuration, auth.CertVaultPath, CertPasswordKeys);
                }
                catch (InvalidOperationException)
                {
                    password = string.Empty;
                }
            }

            return LoadFromBase64(raw, password);
        }

        var path = ResolveFile(auth.CertPath, configuration, environment);
        if (path is null)
        {
            throw new InvalidOperationException(MissingFileMessage(serviceName, auth));
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

    public static string MissingFileMessage(ServiceEntity service) =>
        MissingFileMessage(service.Name, ServiceAuthResolver.Resolve(service));

    public static string MissingFileMessage(string serviceName, ServiceAuthContext auth)
    {
        if (!string.IsNullOrWhiteSpace(auth.CertVaultPath))
        {
            return $"No PFX for {serviceName}: Vault path '{auth.CertVaultPath}'.";
        }

        if (!string.IsNullOrWhiteSpace(auth.CertBase64))
        {
            return $"No PFX for {serviceName}: cert_base64 is invalid.";
        }

        var name = string.IsNullOrWhiteSpace(auth.CertPath)
            ? "client.pfx"
            : Path.GetFileName(auth.CertPath.Replace('\\', '/'));
        return $"No PFX for {serviceName}. Put {name} in {WindowsHostDirectory}, set auth.cert_base64 / api_auth.cert_base64, or cert_vault.";
    }
}
