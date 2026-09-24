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

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(filePath));
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Array || root.EnumerateArray().Any(e => e.ValueKind != JsonValueKind.Object))
            throw new InvalidDataException(DescribeShape(root, filePath));

        var rows = root.EnumerateArray().ToList();
        if (rows.Count == 0)
            return ([], []);

        var headers = rows[0].EnumerateObject().Select(p => p.Name).ToList();
        var records = rows.Select(obj => obj.EnumerateObject().ToDictionary(
                p => p.Name,
                p => p.Value.ValueKind == JsonValueKind.Null ? string.Empty : p.Value.ToString()))
            .ToList();

        return (records, headers);
    }

    // A table is one level: rows, then fields. Documents that keep their rows under a property
    // ({"data": [...]}) are common, and the parser's own message ("could not be converted to
    // List<Dictionary<...>>") says nothing a person can act on, so say what was found instead.
    private static string DescribeShape(JsonElement root, string filePath)
    {
        const string expected = "A JSON file is read as a table when it is an array of objects, one per row";
        if (root.ValueKind == JsonValueKind.Object)
        {
            var arrays = root.EnumerateObject()
                .Where(p => p.Value.ValueKind == JsonValueKind.Array)
                .Select(p => $"'{p.Name}' ({p.Value.GetArrayLength()} items)")
                .ToList();
            var where = arrays.Count > 0
                ? $"; its rows may be under {string.Join(", ", arrays)} — extract that array into its own file"
                : "";
            return $"{expected}, but {Path.GetFileName(filePath)} is an object{where}.";
        }

        return root.ValueKind == JsonValueKind.Array
            ? $"{expected}, but {Path.GetFileName(filePath)} is an array whose items are not all objects."
            : $"{expected}, but {Path.GetFileName(filePath)} holds a single {root.ValueKind.ToString().ToLowerInvariant()}.";
    }
}
