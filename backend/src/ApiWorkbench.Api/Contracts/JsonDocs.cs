using System.Text.Json;

namespace ApiWorkbench.Api.Contracts;

internal static class JsonDocs
{
    public static JsonElement? ToElement(JsonDocument? document)
    {
        if (document is null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<JsonElement>(document.RootElement.GetRawText());
    }

    public static JsonDocument? ParseOrNull(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
