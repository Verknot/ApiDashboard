using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ApiWorkbench.Api.Data;
using Microsoft.EntityFrameworkCore;
using Scriban;

namespace ApiWorkbench.Api.Services;

public interface IDtoGeneratorService
{
    Task<(string FileName, string Content)> GenerateAsync(int endpointId, CancellationToken cancellationToken = default);
}

public sealed class DtoGeneratorService(
    AppDbContext db,
    IHostEnvironment environment) : IDtoGeneratorService
{
    public async Task<(string FileName, string Content)> GenerateAsync(int endpointId, CancellationToken cancellationToken = default)
    {
        var endpoint = await db.Endpoints
            .Include(e => e.Service)
            .FirstOrDefaultAsync(e => e.Id == endpointId, cancellationToken)
            ?? throw new KeyNotFoundException("Эндпоинт не найден.");

        var schema = endpoint.RequestSchema ?? endpoint.ResponseSchema;
        var className = ToPascal(endpoint.OperationId ?? $"{endpoint.Method}_{endpoint.Path}") + "Dto";
        var properties = schema is null ? [] : Flatten(schema.RootElement);

        var templatePath = Path.Combine(environment.ContentRootPath, "Templates", "dto.scriban");
        if (!File.Exists(templatePath))
        {
            templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "dto.scriban");
        }

        var templateText = await File.ReadAllTextAsync(templatePath, Encoding.UTF8, cancellationToken);
        var template = Template.Parse(templateText);
        if (template.HasErrors)
        {
            throw new InvalidOperationException(string.Join("; ", template.Messages.Select(m => m.Message)));
        }

        var ns = ToPascal(endpoint.Service.Name.Replace("-service", "", StringComparison.OrdinalIgnoreCase)) + ".Contracts";
        var rendered = await template.RenderAsync(new
        {
            @namespace = ns,
            description = endpoint.Description ?? $"{endpoint.Method} {endpoint.Path}",
            class_name = className,
            properties
        });

        return ($"{className}.cs", rendered.Replace("\r\n", "\n"));
    }

    private static List<object> Flatten(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        if (!schema.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var required = new HashSet<string>(StringComparer.Ordinal);
        if (schema.TryGetProperty("required", out var req) && req.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in req.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    required.Add(item.GetString()!);
                }
            }
        }

        var list = new List<object>();
        foreach (var property in properties.EnumerateObject())
        {
            var (type, nullable) = MapType(property.Value);
            list.Add(new
            {
                json_name = property.Name,
                name = ToPascal(property.Name),
                type,
                is_nullable = nullable && !required.Contains(property.Name)
            });
        }

        return list;
    }

    private static (string Type, bool Nullable) MapType(JsonElement schema)
    {
        var nullable = false;
        if (schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("nullable", out var flag)
            && flag.ValueKind is JsonValueKind.True)
        {
            nullable = true;
        }

        var type = schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("type", out var t)
            ? t.GetString()
            : null;

        return type switch
        {
            "string" when schema.TryGetProperty("format", out var fmt) && fmt.GetString() == "date-time"
                => ("DateTimeOffset", true),
            "string" => ("string", true),
            "integer" => ("int", nullable),
            "number" => ("decimal", nullable),
            "boolean" => ("bool", nullable),
            "array" => ("List<object>", true),
            "object" => ("JsonElement", true),
            _ => ("JsonElement", true)
        };
    }

    private static string ToPascal(string value)
    {
        var tokens = Regex.Matches(value, "[A-Z]+(?![a-z])|[A-Z][a-z]+|[a-z]+|[0-9]+")
            .Select(match => match.Value)
            .Where(token => token.Length > 0)
            .ToList();
        if (tokens.Count == 0)
        {
            return "Dto";
        }

        return string.Concat(tokens.Select(token =>
            char.ToUpper(token[0], CultureInfo.InvariantCulture) + token[1..].ToLowerInvariant()));
    }
}
