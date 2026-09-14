using System.Text.Json;
using Microsoft.OpenApi.Models;

namespace ApiWorkbench.Api.Services;

internal static class OpenApiSchemaSupport
{
    public static OpenApiSchema? Resolve(OpenApiDocument document, OpenApiSchema? schema)
    {
        var current = schema;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (current?.Reference?.Id is { Length: > 0 } id && seen.Add(id))
        {
            if (document.Components?.Schemas != null
                && document.Components.Schemas.TryGetValue(id, out var next)
                && next is not null)
            {
                current = next;
                continue;
            }

            break;
        }

        return current;
    }

    public static OpenApiSchema? Inline(OpenApiDocument document, OpenApiSchema? schema) =>
        Inline(document, schema, new HashSet<string>(StringComparer.Ordinal));

    private static OpenApiSchema? Inline(OpenApiDocument document, OpenApiSchema? schema, HashSet<string> seen)
    {
        if (schema is null)
        {
            return null;
        }

        var current = schema;
        if (current.Reference?.Id is { Length: > 0 } id)
        {
            if (!seen.Add(id))
            {
                return new OpenApiSchema { Type = "object" };
            }

            current = Resolve(document, current) ?? current;
        }

        current.Reference = null;
        current.UnresolvedReference = false;

        if (current.Properties is { Count: > 0 })
        {
            foreach (var key in current.Properties.Keys.ToList())
            {
                current.Properties[key] = Inline(document, current.Properties[key], seen) ?? current.Properties[key];
            }
        }

        if (current.Items is not null)
        {
            current.Items = Inline(document, current.Items, seen);
        }

        if (current.AllOf is { Count: > 0 })
        {
            current.AllOf = current.AllOf.Select(part => Inline(document, part, seen) ?? part).ToList();
        }

        if (current.OneOf is { Count: > 0 })
        {
            current.OneOf = current.OneOf.Select(part => Inline(document, part, seen) ?? part).ToList();
        }

        if (current.AnyOf is { Count: > 0 })
        {
            current.AnyOf = current.AnyOf.Select(part => Inline(document, part, seen) ?? part).ToList();
        }

        if (current.AdditionalProperties is not null)
        {
            current.AdditionalProperties = Inline(document, current.AdditionalProperties, seen) ?? current.AdditionalProperties;
        }

        return current;
    }

    public static JsonElement? ResolveElement(JsonElement? schema, JsonElement? document)
    {
        if (schema is null || schema.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return schema;
        }

        return ResolveElement(schema.Value, document, 0);
    }

    public static JsonElement? ExampleElement(JsonElement? schema, JsonElement? document)
    {
        var resolved = ResolveElement(schema, document);
        if (resolved is null)
        {
            return null;
        }

        var example = BuildExample(resolved.Value, document, 0);
        if (example is string)
        {
            example = new Dictionary<string, object?>();
        }

        return JsonSerializer.SerializeToElement(example);
    }

    public static OpenApiMediaType? PickJsonContent(IDictionary<string, OpenApiMediaType>? content)
    {
        if (content is null || content.Count == 0)
        {
            return null;
        }

        foreach (var (key, value) in content)
        {
            if (key.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return content.Values.FirstOrDefault();
    }

    private static JsonElement ResolveElement(JsonElement schema, JsonElement? document, int depth)
    {
        if (depth > 10 || schema.ValueKind != JsonValueKind.Object)
        {
            return schema;
        }

        if (schema.TryGetProperty("$ref", out var reference) && reference.ValueKind == JsonValueKind.String)
        {
            var target = Lookup(document, reference.GetString());
            if (target is not null)
            {
                return ResolveElement(target.Value, document, depth + 1);
            }
        }

        if (schema.TryGetProperty("allOf", out var allOf) && allOf.ValueKind == JsonValueKind.Array)
        {
            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                writer.WriteString("type", "object");
                writer.WritePropertyName("properties");
                writer.WriteStartObject();
                foreach (var part in allOf.EnumerateArray())
                {
                    var resolved = ResolveElement(part, document, depth + 1);
                    if (resolved.ValueKind == JsonValueKind.Object
                        && resolved.TryGetProperty("properties", out var props)
                        && props.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var property in props.EnumerateObject())
                        {
                            writer.WritePropertyName(property.Name);
                            property.Value.WriteTo(writer);
                        }
                    }
                }

                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            return JsonDocument.Parse(buffer.ToArray()).RootElement.Clone();
        }

        return schema;
    }

    private static JsonElement? Lookup(JsonElement? document, string? pointer)
    {
        if (document is null || string.IsNullOrWhiteSpace(pointer) || !pointer.StartsWith("#/", StringComparison.Ordinal))
        {
            return null;
        }

        var current = document.Value;
        foreach (var segment in pointer[2..].Split('/'))
        {
            var name = Uri.UnescapeDataString(segment.Replace("~1", "/").Replace("~0", "~"));
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(name, out current))
            {
                return null;
            }
        }

        return current.Clone();
    }

    private static object? BuildExample(JsonElement schema, JsonElement? document, int depth, string name = "")
    {
        if (depth > 10)
        {
            return new Dictionary<string, object?>();
        }

        var resolved = ResolveElement(schema, document, depth);
        if (resolved.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, object?>();
        }

        if (resolved.TryGetProperty("example", out var example))
        {
            return JsonSerializer.Deserialize<object>(example.GetRawText());
        }

        if (resolved.TryGetProperty("default", out var defaultValue))
        {
            return JsonSerializer.Deserialize<object>(defaultValue.GetRawText());
        }

        if (resolved.TryGetProperty("enum", out var enums) && enums.ValueKind == JsonValueKind.Array)
        {
            var first = enums.EnumerateArray().FirstOrDefault();
            if (first.ValueKind is not JsonValueKind.Undefined)
            {
                return JsonSerializer.Deserialize<object>(first.GetRawText());
            }
        }

        var type = resolved.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String
            ? typeEl.GetString()
            : null;

        if (type == "array")
        {
            var items = resolved.TryGetProperty("items", out var itemsEl) ? itemsEl : default;
            return new object?[] { items.ValueKind == JsonValueKind.Object ? BuildExample(items, document, depth + 1, name) : null };
        }

        if (type == "object" || resolved.TryGetProperty("properties", out _))
        {
            var result = new Dictionary<string, object?>();
            if (resolved.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in properties.EnumerateObject())
                {
                    result[property.Name] = BuildExample(property.Value, document, depth + 1, property.Name);
                }
            }

            return result;
        }

        if (type is "integer" or "number")
        {
            return 0;
        }

        if (type == "boolean")
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(name) ? "" : name;
    }
}
