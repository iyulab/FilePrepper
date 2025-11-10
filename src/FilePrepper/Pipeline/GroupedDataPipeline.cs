using FilePrepper.Tasks.WindowOps;

namespace FilePrepper.Pipeline;

/// <summary>
/// Represents a DataPipeline grouped by a key column, ready for aggregation operations
/// </summary>
public class GroupedDataPipeline
{
    private readonly List<Dictionary<string, string>> _rows;
    private readonly List<string> _columnNames;
    private readonly string _keyColumn;

    internal GroupedDataPipeline(
        List<Dictionary<string, string>> rows,
        List<string> columnNames,
        string keyColumn)
    {
        _rows = rows;
        _columnNames = columnNames;
        _keyColumn = keyColumn;
    }

    /// <summary>
    /// Apply aggregation functions to grouped data
    /// </summary>
    /// <param name="aggregations">Array of (columnName, method) tuples defining aggregations to perform</param>
    /// <param name="keepKey">Include grouping key in result (default: true)</param>
    /// <param name="suffixFormat">Suffix format for output columns. Use {method} placeholder (default: "_{method}")</param>
    /// <returns>New DataPipeline with aggregated results</returns>
    /// <exception cref="ArgumentException">Thrown when aggregation column doesn't exist</exception>
    public DataPipeline Aggregate(
        (string column, AggregationMethod method)[] aggregations,
        bool keepKey = true,
        string? suffixFormat = "_{method}")
    {
        if (aggregations == null || aggregations.Length == 0)
        {
            throw new ArgumentException("At least one aggregation must be specified", nameof(aggregations));
        }

        // Validate all aggregation columns exist
        foreach (var (column, _) in aggregations)
        {
            if (!_columnNames.Contains(column))
            {
                throw new ArgumentException(
                    $"Column '{column}' not found. Available columns: {string.Join(", ", _columnNames)}",
                    nameof(aggregations));
            }
        }

        // Group rows by key column
        var groups = _rows
            .Where(row => row.ContainsKey(_keyColumn)) // Filter out rows without key
            .GroupBy(row => row[_keyColumn])
            .Where(g => !string.IsNullOrEmpty(g.Key)) // Exclude empty/null keys
            .ToList();

        var resultRows = new List<Dictionary<string, string>>();
        var resultColumns = new List<string>();

        // Add key column if requested
        if (keepKey)
        {
            resultColumns.Add(_keyColumn);
        }

        // Build result column names from aggregations
        foreach (var (column, method) in aggregations)
        {
            var suffix = suffixFormat?.Replace("{method}", method.ToString().ToLower())
                         ?? $"_{method.ToString().ToLower()}";
            resultColumns.Add($"{column}{suffix}");
        }

        // Process each group
        foreach (var group in groups.OrderBy(g => g.Key)) // Sort by key for consistent output
        {
            var resultRow = new Dictionary<string, string>();

            // Add key column value
            if (keepKey)
            {
                resultRow[_keyColumn] = group.Key;
            }

            // Apply each aggregation
            foreach (var (column, method) in aggregations)
            {
                var suffix = suffixFormat?.Replace("{method}", method.ToString().ToLower())
                             ?? $"_{method.ToString().ToLower()}";
                var outputColumn = $"{column}{suffix}";

                // Extract numeric values from the group
                var values = group
                    .Select(row => row.TryGetValue(column, out var v) ? v : string.Empty)
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Where(v => double.TryParse(v, out _))
                    .Select(double.Parse)
                    .ToList();

                if (values.Any())
                {
                    var aggregatedValue = CalculateAggregation(values, method);
                    resultRow[outputColumn] = aggregatedValue.ToString("G");
                }
                else
                {
                    // No valid values - store empty string
                    resultRow[outputColumn] = string.Empty;
                }
            }

            resultRows.Add(resultRow);
        }

        return DataPipeline.FromData(resultRows);
    }

    /// <summary>
    /// Calculate aggregation value for a list of numeric values
    /// </summary>
    private static double CalculateAggregation(List<double> values, AggregationMethod method)
    {
        return method switch
        {
            AggregationMethod.Mean => values.Average(),
            AggregationMethod.Sum => values.Sum(),
            AggregationMethod.Min => values.Min(),
            AggregationMethod.Max => values.Max(),
            AggregationMethod.Count => values.Count,
            AggregationMethod.Std => CalculateStandardDeviation(values),
            AggregationMethod.Var => CalculateVariance(values),
            AggregationMethod.Median => CalculateMedian(values),
            AggregationMethod.First => values.First(),
            AggregationMethod.Last => values.Last(),
            _ => throw new NotImplementedException($"Aggregation method {method} is not implemented")
        };
    }

    /// <summary>
    /// Calculate standard deviation using sample standard deviation formula (n-1 denominator)
    /// </summary>
    private static double CalculateStandardDeviation(List<double> values)
    {
        if (values.Count <= 1)
        {
            return 0;
        }

        var mean = values.Average();
        var sumOfSquares = values.Sum(v => Math.Pow(v - mean, 2));
        return Math.Sqrt(sumOfSquares / (values.Count - 1));
    }

    /// <summary>
    /// Calculate variance using sample variance formula (n-1 denominator)
    /// </summary>
    private static double CalculateVariance(List<double> values)
    {
        if (values.Count <= 1)
        {
            return 0;
        }

        var mean = values.Average();
        return values.Sum(v => Math.Pow(v - mean, 2)) / (values.Count - 1);
    }

    /// <summary>
    /// Calculate median value
    /// </summary>
    private static double CalculateMedian(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        int n = sorted.Count;

        if (n % 2 == 1)
        {
            return sorted[n / 2];
        }
        else
        {
            return (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
        }
    }
}
