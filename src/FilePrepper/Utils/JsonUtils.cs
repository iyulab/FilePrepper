using System.Text.Json;

namespace FilePrepper.Utils;

/// <summary>Reads JSON into the same record shape as CSV input.</summary>
public static class JsonUtils
{
    /// <summary>
    /// Reads <paramref name="filePath"/> as a table. Without <paramref name="recordPath"/> the file must be
    /// an array of objects, one per row. With it, the rows are the objects of the array the dotted path
    /// leads to — <c>data.paragraphs.qas</c> walks <c>data[]</c>, then each item's <c>paragraphs[]</c>,
    /// then each of those items' <c>qas[]</c> — and each row also carries the other fields of every item
    /// it was reached through, named by the array that item came from (<c>paragraphs.context</c>). That is
    /// how a question keeps its passage when the two are stored at different levels. Fields holding objects
    /// or arrays become one column of JSON text.
    /// </summary>
    public static async Task<(List<Dictionary<string, string>> records, List<string> headers)> ReadJsonFileAsync(
        string filePath, string? recordPath = null)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"JSON file not found: {filePath}");

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(filePath));
        var root = document.RootElement;

        if (!string.IsNullOrWhiteSpace(recordPath))
            return ReadRecordPath(root, recordPath, filePath);

        if (root.ValueKind != JsonValueKind.Array || root.EnumerateArray().Any(e => e.ValueKind != JsonValueKind.Object))
            throw new InvalidDataException(DescribeShape(root, filePath));

        var rows = root.EnumerateArray().ToList();
        if (rows.Count == 0)
            return ([], []);

        var headers = rows[0].EnumerateObject().Select(p => p.Name).ToList();
        var records = rows.Select(obj => obj.EnumerateObject().ToDictionary(p => p.Name, p => Text(p.Value))).ToList();

        return (records, headers);
    }

    private static (List<Dictionary<string, string>> records, List<string> headers) ReadRecordPath(
        JsonElement root, string recordPath, string filePath)
    {
        var segments = recordPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var name = Path.GetFileName(filePath);

        // A top-level array is a list of documents; each is walked as a root object would be.
        var level = root.ValueKind == JsonValueKind.Array
            ? root.EnumerateArray().Select(e => (Item: e, Carried: new List<KeyValuePair<string, string>>())).ToList()
            : [(root, new List<KeyValuePair<string, string>>())];

        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            var walked = string.Join('.', segments.Take(i + 1));
            var next = new List<(JsonElement Item, List<KeyValuePair<string, string>> Carried)>();
            foreach (var (item, carried) in level)
            {
                if (item.ValueKind != JsonValueKind.Object)
                    throw new InvalidDataException(
                        $"{name}: '{walked}' is read from each item of '{string.Join('.', segments.Take(i))}', but an item there is a {Kind(item)}, not an object.");
                if (!item.TryGetProperty(segment, out var array) || array.ValueKind != JsonValueKind.Array)
                    throw new InvalidDataException(
                        $"{name}: the record path '{recordPath}' needs an array at '{walked}', but " +
                        (array.ValueKind == JsonValueKind.Undefined ? "an item there has no such field." : $"found a {Kind(array)}.") +
                        Candidates(item, " Arrays at that level: "));

                // The fields beside the array go along with every row reached through it. The root's own
                // fields (a file's version or creator) do not: they say nothing about any one row.
                var withThisLevel = i == 0
                    ? carried
                    : [.. carried, .. item.EnumerateObject()
                        .Where(p => p.Name != segment)
                        .Select(p => new KeyValuePair<string, string>($"{segments[i - 1]}.{p.Name}", Text(p.Value)))];

                foreach (var child in array.EnumerateArray())
                    next.Add((child, withThisLevel));
            }
            level = next;
        }

        var headers = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var records = new List<Dictionary<string, string>>(level.Count);
        foreach (var (item, carried) in level)
        {
            if (item.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException(
                    $"{name}: the record path '{recordPath}' leads to an array whose items are {Kind(item)}s; each row has to be an object.");

            var row = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (key, value) in carried)
                row[key] = value;
            foreach (var p in item.EnumerateObject())
            {
                if (row.ContainsKey(p.Name))
                    throw new InvalidDataException($"{name}: the column '{p.Name}' is both a row field and a field of an enclosing item.");
                row[p.Name] = Text(p.Value);
            }

            foreach (var key in row.Keys.Where(seen.Add))
                headers.Add(key);
            records.Add(row);
        }

        // A field some rows lack is an empty cell in those rows, as a missing value is in CSV.
        foreach (var row in records)
            foreach (var header in headers)
                row.TryAdd(header, string.Empty);

        return (records, headers);
    }

    private static string Text(JsonElement value) =>
        value.ValueKind == JsonValueKind.Null ? string.Empty : value.ToString();

    private static string Kind(JsonElement value) => value.ValueKind.ToString().ToLowerInvariant();

    private static string Candidates(JsonElement item, string lead)
    {
        var arrays = item.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Array).Select(p => $"'{p.Name}'").ToList();
        return arrays.Count > 0 ? lead + string.Join(", ", arrays) + "." : "";
    }

    // A table is one level: rows, then fields. Documents that keep their rows under a property
    // ({"data": [...]}) are common, and the parser's own message ("could not be converted to
    // List<Dictionary<...>>") says nothing a person can act on, so say what was found instead —
    // including the record paths that would read it, however deep the rows are.
    private static string DescribeShape(JsonElement root, string filePath)
    {
        const string expected = "A JSON file is read as a table when it is an array of objects, one per row";
        if (root.ValueKind == JsonValueKind.Object)
        {
            var arrays = root.EnumerateObject()
                .Where(p => p.Value.ValueKind == JsonValueKind.Array)
                .Select(p => $"'{p.Name}' ({p.Value.GetArrayLength()} items)")
                .ToList();
            var paths = RecordPaths(root, "", depth: 0).ToList();
            var where = arrays.Count > 0
                ? $"; its rows may be under {string.Join(", ", arrays)}"
                  + (paths.Count > 0 ? $" — read it with a record path: {string.Join(", ", paths.Select(p => $"'{p}'"))}" : "")
                : "";
            return $"{expected}, but {Path.GetFileName(filePath)} is an object{where}.";
        }

        return root.ValueKind == JsonValueKind.Array
            ? $"{expected}, but {Path.GetFileName(filePath)} is an array whose items are not all objects."
            : $"{expected}, but {Path.GetFileName(filePath)} holds a single {Kind(root)}.";
    }

    // Dotted paths to arrays of objects, following the first item at each level — enough to name the
    // choices a person has, not a schema.
    private static IEnumerable<string> RecordPaths(JsonElement item, string prefix, int depth)
    {
        if (depth > 5 || item.ValueKind != JsonValueKind.Object)
            yield break;
        foreach (var p in item.EnumerateObject())
        {
            if (p.Value.ValueKind != JsonValueKind.Array || p.Value.GetArrayLength() == 0)
                continue;
            var first = p.Value[0];
            if (first.ValueKind != JsonValueKind.Object)
                continue;
            var path = prefix.Length == 0 ? p.Name : $"{prefix}.{p.Name}";
            yield return path;
            foreach (var deeper in RecordPaths(first, path, depth + 1))
                yield return deeper;
        }
    }
}
