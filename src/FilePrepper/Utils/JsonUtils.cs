using System.Text.Json;

namespace FilePrepper.Utils;

/// <summary>Reads a JSON array of objects into the same record shape as CSV input.</summary>
public static class JsonUtils
{
    public static async Task<(List<Dictionary<string, string>> records, List<string> headers)> ReadJsonFileAsync(
        string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"JSON file not found: {filePath}");

        var jsonContent = await File.ReadAllTextAsync(filePath);
        var jsonArray = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(jsonContent);

        if (jsonArray == null || jsonArray.Count == 0)
            return ([], []);

        var headers = jsonArray[0].Keys.ToList();
        var records = jsonArray.Select(obj =>
            obj.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ValueKind == JsonValueKind.Null ? string.Empty : kvp.Value.ToString()))
            .ToList();

        return (records, headers);
    }
}
