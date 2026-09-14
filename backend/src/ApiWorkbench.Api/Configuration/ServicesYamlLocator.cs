namespace ApiWorkbench.Api.Configuration;

public static class ServicesYamlLocator
{
    public static string Resolve(IConfiguration configuration, IHostEnvironment? environment = null)
    {
        var configured = configuration["ServicesConfigPath"] ?? "config/services.yaml";
        if (Path.IsPathRooted(configured))
        {
            return configured;
        }

        var found = new List<string>();
        AddIfExists(found, Path.GetFullPath(configured));
        AddIfExists(found, Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configured)));
        if (environment is not null)
        {
            AddIfExists(found, Path.GetFullPath(Path.Combine(environment.ContentRootPath, configured)));
        }

        foreach (var start in new[]
                 {
                     Directory.GetCurrentDirectory(),
                     environment?.ContentRootPath,
                     AppContext.BaseDirectory
                 }.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            var dir = new DirectoryInfo(start!);
            while (dir is not null)
            {
                AddIfExists(found, Path.Combine(dir.FullName, "config", "services.yaml"));
                dir = dir.Parent;
            }
        }

        var source = found.FirstOrDefault(path => !IsBuildOutput(path));
        return source ?? found.FirstOrDefault() ?? Path.GetFullPath(configured);
    }

    private static void AddIfExists(List<string> found, string path)
    {
        var full = Path.GetFullPath(path);
        if (File.Exists(full) && !found.Contains(full, StringComparer.OrdinalIgnoreCase))
        {
            found.Add(full);
        }
    }

    private static bool IsBuildOutput(string path)
    {
        var normalized = path.Replace('/', Path.DirectorySeparatorChar);
        var bin = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}";
        var obj = $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}";
        return normalized.Contains(bin, StringComparison.OrdinalIgnoreCase)
               || normalized.Contains(obj, StringComparison.OrdinalIgnoreCase);
    }
}
