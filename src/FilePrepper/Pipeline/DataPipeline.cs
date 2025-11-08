using CsvHelper;
using CsvHelper.Configuration;
using FilePrepper.Tasks.NormalizeData;
using System.Globalization;

namespace FilePrepper.Pipeline;

/// <summary>
/// Fluent API for chaining data transformations without file I/O overhead
/// </summary>
public class DataPipeline
{
    private readonly List<Dictionary<string, string>> _rows;
    private readonly List<string> _columnNames;

    private DataPipeline(IEnumerable<Dictionary<string, string>> rows, IEnumerable<string> columnNames)
    {
        _rows = rows.ToList();
        _columnNames = columnNames.ToList();
    }

    #region Factory Methods

    /// <summary>
    /// Create pipeline from CSV file
    /// </summary>
    public static async Task<DataPipeline> FromCsvAsync(string path, bool hasHeader = true)
    {
        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = hasHeader
        });

        var rows = new List<Dictionary<string, string>>();
        var headers = new List<string>();

        await csv.ReadAsync();
        csv.ReadHeader();
        headers.AddRange(csv.HeaderRecord ?? Array.Empty<string>());

        while (await csv.ReadAsync())
        {
            var row = new Dictionary<string, string>();
            foreach (var header in headers)
            {
                row[header] = csv.GetField(header) ?? string.Empty;
            }
            rows.Add(row);
        }

        return new DataPipeline(rows, headers);
    }

    /// <summary>
    /// Create pipeline from in-memory data
    /// </summary>
    public static DataPipeline FromData(IEnumerable<Dictionary<string, string>> data)
    {
        var dataList = data.ToList();
        if (!dataList.Any())
        {
            return new DataPipeline(Enumerable.Empty<Dictionary<string, string>>(), Enumerable.Empty<string>());
        }

        var columns = dataList.First().Keys.ToList();
        return new DataPipeline(dataList, columns);
    }

    #endregion

    #region Properties

    public int RowCount => _rows.Count;
    public IReadOnlyList<string> ColumnNames => _columnNames.AsReadOnly();

    #endregion

    #region Transformation Methods

    /// <summary>
    /// Add a computed column
    /// </summary>
    public DataPipeline AddColumn(string columnName, Func<Dictionary<string, string>, string> valueSelector)
    {
        if (!_columnNames.Contains(columnName))
        {
            _columnNames.Add(columnName);
        }

        foreach (var row in _rows)
        {
            row[columnName] = valueSelector(row);
        }

        return this;
    }

    /// <summary>
    /// Remove columns
    /// </summary>
    public DataPipeline RemoveColumns(params string[] columnNames)
    {
        foreach (var colName in columnNames)
        {
            _columnNames.Remove(colName);
            foreach (var row in _rows)
            {
                row.Remove(colName);
            }
        }

        return this;
    }

    /// <summary>
    /// Rename a column
    /// </summary>
    public DataPipeline RenameColumn(string oldName, string newName)
    {
        var index = _columnNames.IndexOf(oldName);
        if (index >= 0)
        {
            _columnNames[index] = newName;
        }

        foreach (var row in _rows)
        {
            if (row.TryGetValue(oldName, out var value))
            {
                row.Remove(oldName);
                row[newName] = value;
            }
        }

        return this;
    }

    /// <summary>
    /// Filter rows by predicate
    /// </summary>
    public DataPipeline FilterRows(Func<Dictionary<string, string>, bool> predicate)
    {
        var filteredRows = _rows.Where(predicate).ToList();
        return new DataPipeline(filteredRows, _columnNames);
    }

    /// <summary>
    /// Normalize numeric columns (Min-Max or Z-Score)
    /// </summary>
    public DataPipeline Normalize(string[] columns, NormalizationMethod method, double minValue = 0, double maxValue = 1)
    {
        var stats = CalculateColumnStats(columns);

        foreach (var row in _rows)
        {
            foreach (var col in columns)
            {
                if (!row.TryGetValue(col, out var valueStr) || !double.TryParse(valueStr, out var value))
                {
                    continue;
                }

                var (min, max, mean, stdDev) = stats[col];
                double normalized;

                if (method == NormalizationMethod.MinMax)
                {
                    var range = max - min;
                    normalized = Math.Abs(range) < 1e-12
                        ? minValue
                        : ((value - min) / range) * (maxValue - minValue) + minValue;
                }
                else // ZScore
                {
                    normalized = Math.Abs(stdDev) < 1e-12
                        ? mean
                        : (value - mean) / stdDev;
                }

                row[col] = normalized.ToString("G");
            }
        }

        return this;
    }

    /// <summary>
    /// Fill missing values
    /// </summary>
    public DataPipeline FillMissing(string[] columns, FillMethod method, string? constantValue = null)
    {
        foreach (var col in columns)
        {
            var validValues = _rows
                .Select(row => row.TryGetValue(col, out var v) ? v : string.Empty)
                .Where(v => !string.IsNullOrWhiteSpace(v) && double.TryParse(v, out _))
                .Select(double.Parse)
                .ToList();

            if (!validValues.Any())
            {
                continue;
            }

            string fillValue = method switch
            {
                FillMethod.Mean => validValues.Average().ToString("G"),
                FillMethod.Median => CalculateMedian(validValues).ToString("G"),
                FillMethod.Constant when constantValue != null => constantValue,
                _ => "0"
            };

            foreach (var row in _rows)
            {
                if (!row.TryGetValue(col, out var value) || string.IsNullOrWhiteSpace(value))
                {
                    row[col] = fillValue;
                }
            }
        }

        return this;
    }

    #endregion

    #region Output Methods

    /// <summary>
    /// Get all values from a column
    /// </summary>
    public IEnumerable<string> GetColumn(string columnName)
    {
        return _rows.Select(row => row.TryGetValue(columnName, out var value) ? value : string.Empty);
    }

    /// <summary>
    /// Get immutable snapshot of current data
    /// </summary>
    public DataFrame ToDataFrame()
    {
        return new DataFrame(_rows, _columnNames);
    }

    /// <summary>
    /// Write to CSV file (only at the end of pipeline)
    /// </summary>
    public async Task ToCsvAsync(string path, bool hasHeader = true)
    {
        await using var writer = new StreamWriter(path);
        await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = hasHeader
        });

        // Write header
        if (hasHeader)
        {
            foreach (var col in _columnNames)
            {
                csv.WriteField(col);
            }
            await csv.NextRecordAsync();
        }

        // Write rows
        foreach (var row in _rows)
        {
            foreach (var col in _columnNames)
            {
                csv.WriteField(row.TryGetValue(col, out var value) ? value : string.Empty);
            }
            await csv.NextRecordAsync();
        }
    }

    #endregion

    #region Helper Methods

    private Dictionary<string, (double min, double max, double mean, double stdDev)> CalculateColumnStats(string[] columns)
    {
        var stats = new Dictionary<string, (double, double, double, double)>();

        foreach (var col in columns)
        {
            var values = _rows
                .Select(row => row.TryGetValue(col, out var v) ? v : string.Empty)
                .Where(v => double.TryParse(v, out _))
                .Select(double.Parse)
                .ToList();

            if (!values.Any())
            {
                stats[col] = (0, 0, 0, 0);
                continue;
            }

            var min = values.Min();
            var max = values.Max();
            var mean = values.Average();
            var variance = values.Average(v => Math.Pow(v - mean, 2));
            var stdDev = Math.Sqrt(variance);

            stats[col] = (min, max, mean, stdDev);
        }

        return stats;
    }

    private static double CalculateMedian(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2
            : sorted[mid];
    }

    #endregion
}

public enum FillMethod
{
    Mean,
    Median,
    Mode,
    Forward,
    Backward,
    Constant
}
