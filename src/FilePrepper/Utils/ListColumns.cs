using System.Text.Json;

namespace FilePrepper.Utils;

/// <summary>
/// Columns whose cells hold a list as JSON text — how a Parquet or JSON list is read
/// (see <see cref="ParquetUtils"/>) — turned into one row per element.
/// </summary>
public static class ListColumns
{
    /// <summary>
    /// One row per element of <paramref name="columns"/>, the other columns repeated on each.
    /// The lists of one row are paired by position, so they must have the same length; an empty
    /// cell or an empty list gives one row with the listed columns empty. An element that is itself
    /// a list or object stays JSON text.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// A column is not in the data, a cell is not a JSON list, or the lists of one row differ in length.
    /// </exception>
    public static List<Dictionary<string, string>> Explode(
        IReadOnlyList<Dictionary<string, string>> rows,
        IReadOnlyCollection<string> headers,
        IReadOnlyList<string> columns)
    {
        if (columns.Count == 0)
            throw new ArgumentException("At least one column to explode must be given.", nameof(columns));
        foreach (var column in columns)
        {
            if (!headers.Contains(column))
                throw new ArgumentException($"Column '{column}' not found in data.", nameof(columns));
        }

        var result = new List<Dictionary<string, string>>(rows.Count);
        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            var lists = columns.Select(c => Elements(row.GetValueOrDefault(c, string.Empty), c, r)).ToList();

            var length = lists[0].Count;
            if (lists.Any(l => l.Count != length))
            {
                var lengths = string.Join(", ", columns.Select((c, i) => $"{c}={lists[i].Count}"));
                throw new ArgumentException(
                    $"Row {r + 1}: the lists to explode must have the same length, but they have {lengths}.",
                    nameof(columns));
            }

            if (length == 0)
            {
                var copy = new Dictionary<string, string>(row);
                foreach (var column in columns)
                    copy[column] = string.Empty;
                result.Add(copy);
                continue;
            }

            for (var e = 0; e < length; e++)
            {
                var copy = new Dictionary<string, string>(row);
                for (var c = 0; c < columns.Count; c++)
                    copy[columns[c]] = lists[c][e];
                result.Add(copy);
            }
        }

        return result;
    }

    private static List<string> Elements(string cell, string column, int rowIndex)
    {
        if (string.IsNullOrWhiteSpace(cell))
            return [];

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(cell);
        }
        catch (JsonException)
        {
            throw new ArgumentException(
                $"Row {rowIndex + 1}, column '{column}': '{Abbreviate(cell)}' is not a JSON list.", nameof(column));
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new ArgumentException(
                    $"Row {rowIndex + 1}, column '{column}': '{Abbreviate(cell)}' is not a JSON list.", nameof(column));

            return document.RootElement.EnumerateArray().Select(Text).ToList();
        }
    }

    private static string Text(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
        _ => element.GetRawText(),
    };

    private static string Abbreviate(string text) => text.Length <= 40 ? text : text[..40] + "…";
}
