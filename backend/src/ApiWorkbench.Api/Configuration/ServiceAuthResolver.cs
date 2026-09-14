using ApiWorkbench.Api.Domain;

namespace ApiWorkbench.Api.Configuration;

/// <summary>
/// Effective API auth for a service root or a named swagger module (api_auth).
/// </summary>
internal sealed record ServiceAuthContext(
    string AuthType,
    string? CertPath,
    string? CertBase64,
    string? CertVaultPath,
    string? CertPassword,
    string TokenField,
    string Module)
{
    public bool HasClientCertificateMaterial =>
        !string.IsNullOrWhiteSpace(CertPath)
        || !string.IsNullOrWhiteSpace(CertBase64)
        || !string.IsNullOrWhiteSpace(CertVaultPath);

    public bool NeedsClientCertificate =>
        string.Equals(AuthType, "certificate", StringComparison.OrdinalIgnoreCase)
        || HasClientCertificateMaterial;
}

internal static class ServiceAuthResolver
{
    public static ServiceAuthContext Resolve(ServiceEntity service, string? module = null)
    {
        var moduleName = module?.Trim() ?? string.Empty;
        ServiceSwaggerSource? source = null;
        if (!string.IsNullOrEmpty(moduleName))
        {
            source = service.SwaggerSources.FirstOrDefault(s =>
                string.Equals(s.Name, moduleName, StringComparison.OrdinalIgnoreCase));
        }

        var hasModuleAuth = source is not null && !string.IsNullOrWhiteSpace(source.ApiAuthType);
        var authType = hasModuleAuth
            ? source!.ApiAuthType!.Trim().ToLowerInvariant()
            : (service.AuthType ?? "none").Trim().ToLowerInvariant();

        return new ServiceAuthContext(
            string.IsNullOrWhiteSpace(authType) ? "none" : authType,
            FirstNonEmpty(source?.CertPath, service.CertPath),
            FirstNonEmpty(source?.CertBase64, service.CertBase64),
            FirstNonEmpty(source?.CertVaultPath, service.CertVaultPath),
            FirstNonEmpty(source?.CertPassword, service.CertPassword),
            FirstNonEmpty(source?.TokenField, service.TokenField) ?? "accessToken",
            moduleName);
    }

    public static string? MatchModuleByUrl(ServiceEntity service, Uri target)
    {
        string? bestModule = null;
        var bestLength = -1;
        foreach (var url in service.Urls)
        {
            if (!Uri.TryCreate(url.BaseUrl, UriKind.Absolute, out var allowed))
            {
                continue;
            }

            if (!string.Equals(allowed.Scheme, target.Scheme, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(allowed.IdnHost, target.IdnHost, StringComparison.OrdinalIgnoreCase)
                || allowed.Port != target.Port)
            {
                continue;
            }

            var length = allowed.AbsoluteUri.TrimEnd('/').Length;
            if (length < bestLength)
            {
                continue;
            }

            bestLength = length;
            bestModule = url.Module ?? string.Empty;
        }

        return bestModule;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.Select(v => v?.Trim()).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
