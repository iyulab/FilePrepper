using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using CsvHelper;
using FilePrepper.Utils;

namespace FilePrepper.Pipeline;

/// <summary>
/// Immutable data container representing tabular data, held by column.
/// </summary>
/// <remarks>
/// A frame is read far more than it is built — analysis takes a column at a time — so it keeps one array of values per
/// column, not a dictionary per row. A dictionary per row cost about twenty times the file on disk (a 22 MB, million-row
/// CSV held about 450 MB of row dictionaries per copy). <see cref="Rows"/> is a view over the columns: a row is read in
/// place, never copied.
/// </remarks>
public class DataFrame
{
    private readonly IReadOnlyList<string> _columnNames;
    private readonly Dictionary<string, int> _columnIndex;
    // Per column, its values in row order; null where a row did not have the column.
    private readonly string?[][] _columns;
    private readonly int _rowCount;

    public DataFrame(IEnumerable<IReadOnlyDictionary<string, string>> rows, IEnumerable<string> columnNames)
    {
        _columnNames = columnNames.ToList().AsReadOnly();
        _columnIndex = IndexOf(_columnNames);

        // The frame owns its values: a pipeline that made it goes on changing its own rows.
        var columns = _columnNames.Select(_ => new List<string?>()).ToArray();
        foreach (var row in rows)
        {
            for (var c = 0; c < columns.Length; c++)
                columns[c].Add(row.TryGetValue(_columnNames[c], out var value) ? value : null);
        }
        _columns = [.. columns.Select(column => column.ToArray())];
        _rowCount = _columns.Length > 0 ? _columns[0].Length : rows.Count();
    }

    private DataFrame(IReadOnlyList<string> columnNames, string?[][] columns, int rowCount)
    {
        _columnNames = columnNames;
        _columnIndex = IndexOf(columnNames);
        _columns = columns;
        _rowCount = rowCount;
    }

    /// <summary>
    /// Reads a delimited text file straight into a frame — no row dictionaries in between, so the memory is the values'
    /// own. Encoding and delimiter are detected as <see cref="DataPipeline.FromCsvAsync"/> detects them, and the frame
    /// equals <c>(await DataPipeline.FromCsvAsync(path)).ToDataFrame()</c>.
    /// </summary>
    /// <param name="path">CSV or TSV file path.</param>
    /// <param name="encoding">File encoding name, or "auto" to auto-detect.</param>
    public static async Task<DataFrame> FromCsvAsync(string path, string encoding = "auto")
    {
        using var reader = CsvUtils.CreateReader(path, encoding);
        using var csv = new CsvReader(reader, CsvUtils.GetDefaultConfiguration(hasHeader: true, DataFileFormats.DelimiterFor(path)));

        await csv.ReadAsync();
        csv.ReadHeader();
        var headers = (csv.HeaderRecord ?? []).ToList();
        var columns = headers.Select(_ => new List<string?>()).ToArray();
        var rowCount = 0;

        while (await csv.ReadAsync())
        {
            for (var c = 0; c < columns.Length; c++)
                columns[c].Add(csv.GetField(headers[c]) ?? string.Empty);
            rowCount++;
        }

        return new DataFrame(headers.AsReadOnly(), [.. columns.Select(column => column.ToArray())], rowCount);
    }

    /// <summary>
    /// All rows in the DataFrame, read-only views over its columns — reading them copies nothing however often it
    /// happens. A row holds the columns it was given values for.
    /// </summary>
    public IReadOnlyList<IReadOnlyDictionary<string, string>> Rows => new RowList(this);

    /// <summary>
    /// Column names in the DataFrame
    /// </summary>
    public IReadOnlyList<string> ColumnNames => _columnNames;

    /// <summary>
    /// Number of rows
    /// </summary>
    public int RowCount => _rowCount;

    /// <summary>
    /// Number of columns
    /// </summary>
    public int ColumnCount => _columnNames.Count;

    /// <summary>
    /// Get all values from a specific column — empty where a row has no value for it.
    /// </summary>
    public IEnumerable<string> GetColumn(string columnName)
    {
        if (!_columnIndex.TryGetValue(columnName, out var c))
        {
            throw new KeyNotFoundException($"Column '{columnName}' not found in DataFrame");
        }

        return _columns[c].Select(value => value ?? string.Empty);
    }

    /// <summary>
    /// Select specific columns (projection)
    /// </summary>
    public DataFrame Select(params string[] columnNames)
    {
        // A name the frame does not have selects a column no row has a value for.
        var columns = columnNames
            .Select(name => _columnIndex.TryGetValue(name, out var c) ? _columns[c] : new string?[_rowCount])
            .ToArray();
        return new DataFrame(columnNames.ToList().AsReadOnly(), columns, _rowCount);
    }

    /// <summary>
    /// Filter rows based on a predicate
    /// </summary>
    public DataFrame Where(Func<IReadOnlyDictionary<string, string>, bool> predicate)
    {
        var kept = Enumerable.Range(0, _rowCount).Where(r => predicate(new RowView(this, r))).ToArray();
        var columns = _columns.Select(column => kept.Select(r => column[r]).ToArray()).ToArray();
        return new DataFrame(_columnNames, columns, kept.Length);
    }

    /// <summary>
    /// Serialize to CSV string
    /// </summary>
    public string ToCsv()
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine(string.Join(",", _columnNames));

        // Rows
        for (var r = 0; r < _rowCount; r++)
        {
            var values = _columns.Select(column => column[r] is { } value ? EscapeCsvValue(value) : string.Empty);
            sb.AppendLine(string.Join(",", values));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Serialize to JSON string
    /// </summary>
    public string ToJson()
    {
        var rows = Rows.Select(row => row.ToDictionary(pair => pair.Key, pair => pair.Value));
        return JsonSerializer.Serialize(rows, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private static Dictionary<string, int> IndexOf(IReadOnlyList<string> columnNames)
    {
        var index = new Dictionary<string, int>(columnNames.Count, StringComparer.Ordinal);
        for (var c = 0; c < columnNames.Count; c++)
            index.TryAdd(columnNames[c], c);
        return index;
    }

    private static string EscapeCsvValue(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    private sealed class RowList(DataFrame frame) : IReadOnlyList<IReadOnlyDictionary<string, string>>
    {
        public IReadOnlyDictionary<string, string> this[int index] =>
            (uint)index < (uint)frame._rowCount ? new RowView(frame, index) : throw new ArgumentOutOfRangeException(nameof(index));

        public int Count => frame._rowCount;

        public IEnumerator<IReadOnlyDictionary<string, string>> GetEnumerator()
        {
            for (var r = 0; r < frame._rowCount; r++)
                yield return new RowView(frame, r);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class RowView(DataFrame frame, int row) : IReadOnlyDictionary<string, string>
    {
        public string this[string key] =>
            TryGetValue(key, out var value) ? value : throw new KeyNotFoundException($"Column '{key}' not found in this row");

        public IEnumerable<string> Keys => this.Select(pair => pair.Key);

        public IEnumerable<string> Values => this.Select(pair => pair.Value);

        public int Count => frame._columns.Count(column => column[row] is not null);

        public bool ContainsKey(string key) => TryGetValue(key, out _);

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out string value)
        {
            value = frame._columnIndex.TryGetValue(key, out var c) ? frame._columns[c][row] : null;
            return value is not null;
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            for (var c = 0; c < frame._columns.Length; c++)
            {
                if (frame._columns[c][row] is { } value)
                    yield return new KeyValuePair<string, string>(frame._columnNames[c], value);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
