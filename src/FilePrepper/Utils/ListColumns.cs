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

    /// <summary>
    /// Each of <paramref name="columns"/> replaced, in place, by one column per position of its lists —
    /// <c>features</c> holding <c>[0.5,1]</c> becomes <c>features.0</c> and <c>features.1</c>, named by
    /// path as a Parquet struct's fields are. Every list of a column must have the same length; an
    /// empty cell leaves all its columns empty. An element that is itself a list stays JSON text.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// A column is not in the data, a cell is not a JSON list, a column's lists differ in length, or a
    /// new column's name is already taken.
    /// </exception>
    public static (List<Dictionary<string, string>> Rows, List<string> Headers) SplitToColumns(
        IReadOnlyList<Dictionary<string, string>> rows,
        IReadOnlyList<string> headers,
        IReadOnlyList<string> columns)
    {
        if (columns.Count == 0)
            throw new ArgumentException("At least one list column must be given.", nameof(columns));
        foreach (var column in columns)
        {
            if (!headers.Contains(column))
                throw new ArgumentException($"Column '{column}' not found in data.", nameof(columns));
        }

        var lists = columns.ToDictionary(
            c => c,
            c => rows.Select((row, r) => Elements(row.GetValueOrDefault(c, string.Empty), c, r)).ToList());

        var widths = new Dictionary<string, int>();
        foreach (var column in columns)
        {
            int? width = null;
            for (var r = 0; r < rows.Count; r++)
            {
                if (string.IsNullOrWhiteSpace(rows[r].GetValueOrDefault(column)))
                    continue;
                var count = lists[column][r].Count;
                if (width is null)
                    width = count;
                else if (count != width)
                    throw new ArgumentException(
                        $"Row {r + 1}, column '{column}': a list of {count} where earlier rows have {width}; " +
                        "only lists of one length split into columns.", nameof(columns));
            }
            widths[column] = width ?? 0;
        }

        var newHeaders = new List<string>();
        foreach (var header in headers)
        {
            if (widths.TryGetValue(header, out var width))
                newHeaders.AddRange(Enumerable.Range(0, width).Select(i => $"{header}.{i}"));
            else
                newHeaders.Add(header);
        }
        var clash = newHeaders.GroupBy(h => h, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (clash is not null)
            throw new ArgumentException($"Splitting would make a second column named '{clash.Key}'.", nameof(columns));

        var result = new List<Dictionary<string, string>>(rows.Count);
        for (var r = 0; r < rows.Count; r++)
        {
            var row = new Dictionary<string, string>(newHeaders.Count);
            foreach (var header in headers)
            {
                if (!widths.TryGetValue(header, out var width))
                {
                    row[header] = rows[r].GetValueOrDefault(header, string.Empty);
                    continue;
                }
                var elements = lists[header][r];
                for (var i = 0; i < width; i++)
                    row[$"{header}.{i}"] = i < elements.Count ? elements[i] : string.Empty;
            }
            result.Add(row);
        }

        return (result, newHeaders);
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
