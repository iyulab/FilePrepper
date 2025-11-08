using System.Text;
using System.Text.Json;

namespace FilePrepper.Pipeline;

/// <summary>
/// Immutable data container representing tabular data
/// </summary>
public class DataFrame
{
    private readonly List<Dictionary<string, string>> _rows;
    private readonly IReadOnlyList<string> _columnNames;

    public DataFrame(IEnumerable<Dictionary<string, string>> rows, IEnumerable<string> columnNames)
    {
        // Create defensive copies to ensure immutability
        _rows = rows.Select(row => new Dictionary<string, string>(row)).ToList();
        _columnNames = columnNames.ToList().AsReadOnly();
    }

    /// <summary>
    /// All rows in the DataFrame (defensive copy)
    /// </summary>
    public IReadOnlyList<Dictionary<string, string>> Rows =>
        _rows.Select(row => new Dictionary<string, string>(row)).ToList();

    /// <summary>
    /// Column names in the DataFrame
    /// </summary>
    public IReadOnlyList<string> ColumnNames => _columnNames;

    /// <summary>
    /// Number of rows
    /// </summary>
    public int RowCount => _rows.Count;

    /// <summary>
    /// Number of columns
    /// </summary>
    public int ColumnCount => _columnNames.Count;

    /// <summary>
    /// Get all values from a specific column
    /// </summary>
    public IEnumerable<string> GetColumn(string columnName)
    {
        if (!_columnNames.Contains(columnName))
        {
            throw new KeyNotFoundException($"Column '{columnName}' not found in DataFrame");
        }

        return _rows.Select(row => row.TryGetValue(columnName, out var value) ? value : string.Empty);
    }

    /// <summary>
    /// Select specific columns (projection)
    /// </summary>
    public DataFrame Select(params string[] columnNames)
    {
        var selectedColumns = columnNames.ToList();
        var selectedRows = _rows.Select(row =>
        {
            var newRow = new Dictionary<string, string>();
            foreach (var col in selectedColumns)
            {
                if (row.TryGetValue(col, out var value))
                {
                    newRow[col] = value;
                }
            }
            return newRow;
        });

        return new DataFrame(selectedRows, selectedColumns);
    }

    /// <summary>
    /// Filter rows based on a predicate
    /// </summary>
    public DataFrame Where(Func<Dictionary<string, string>, bool> predicate)
    {
        var filteredRows = _rows.Where(predicate);
        return new DataFrame(filteredRows, _columnNames);
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
        foreach (var row in _rows)
        {
            var values = _columnNames.Select(col =>
                row.TryGetValue(col, out var value) ? EscapeCsvValue(value) : string.Empty);
            sb.AppendLine(string.Join(",", values));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Serialize to JSON string
    /// </summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(_rows, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private static string EscapeCsvValue(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
