using System.Text.Json;

namespace Backend.Services;

public static class JsonDocumentSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, JsonOptions);
    }

    public static T Deserialize<T>(string json, T fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return fallback;
        }

        return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? fallback;
    }

    public static Dictionary<string, Dictionary<string, string>> DeserializeStarterCode(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, Dictionary<string, string>>();
        }

        var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions);
        if (parsed is null)
        {
            return new Dictionary<string, Dictionary<string, string>>();
        }

        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (language, value) in parsed)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                var files = value.Deserialize<Dictionary<string, string>>(JsonOptions);
                result[language] = files ?? new Dictionary<string, string>();
                continue;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                result[language] = new Dictionary<string, string>
                {
                    [GetDefaultFileName(language)] = value.GetString() ?? string.Empty
                };
            }
        }

        return result;
    }

    private static string GetDefaultFileName(string language)
    {
        return language switch
        {
            "javascript" => "main.js",
            "typescript" => "main.ts",
            "html" => "index.html",
            "sql" => "solution.sql",
            _ => "main.py"
        };
    }
}
