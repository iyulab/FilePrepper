namespace FilePrepper.Pipeline;

/// <summary>
/// Statistical summary for a numeric column
/// </summary>
public record ColumnStatistics
{
    /// <summary>
    /// Arithmetic mean (average) of values
    /// </summary>
    public double Mean { get; init; }

    /// <summary>
    /// Standard deviation (sample, n-1 denominator)
    /// </summary>
    public double Std { get; init; }

    /// <summary>
    /// Minimum value
    /// </summary>
    public double Min { get; init; }

    /// <summary>
    /// Maximum value
    /// </summary>
    public double Max { get; init; }

    /// <summary>
    /// Median (50th percentile)
    /// </summary>
    public double Median { get; init; }

    /// <summary>
    /// First quartile (25th percentile)
    /// </summary>
    public double Q1 { get; init; }

    /// <summary>
    /// Third quartile (75th percentile)
    /// </summary>
    public double Q3 { get; init; }

    /// <summary>
    /// Interquartile range (Q3 - Q1)
    /// </summary>
    public double IQR => Q3 - Q1;

    /// <summary>
    /// Number of valid numeric values
    /// </summary>
    public int Count { get; init; }

    /// <summary>
    /// Number of null, empty, or non-numeric values
    /// </summary>
    public int NullCount { get; init; }

    /// <summary>
    /// Variance (sample variance, n-1 denominator)
    /// </summary>
    public double Variance => Std * Std;

    /// <summary>
    /// The statistics of a column's values — the numbers among them; an empty or non-numeric value counts as null.
    /// What <see cref="DataPipeline.GetStatistics"/> computes, for values held anywhere (a <see cref="DataFrame"/>
    /// column, for one), without making a pipeline of the table first.
    /// </summary>
    /// <exception cref="InvalidOperationException">No value is a number.</exception>
    public static ColumnStatistics Of(string column, IEnumerable<string> columnValues)
    {
        var allValues = columnValues as IReadOnlyCollection<string> ?? columnValues.ToList();
        var values = allValues
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Where(v => double.TryParse(v, out _))
            .Select(double.Parse)
            .OrderBy(v => v) // Sort for percentile calculation
            .ToList();

        if (values.Count == 0)
        {
            throw new InvalidOperationException(
                $"Column '{column}' has no valid numeric values. Total rows: {allValues.Count}, " +
                $"Null/Non-numeric: {allValues.Count}");
        }

        var mean = values.Average();
        var variance = values.Count > 1
            ? values.Sum(v => Math.Pow(v - mean, 2)) / (values.Count - 1)
            : 0;

        return new ColumnStatistics
        {
            Mean = mean,
            Std = Math.Sqrt(variance),
            Min = values[0],  // Already sorted
            Max = values[^1],
            Median = CalculatePercentile(values, 0.5),
            Q1 = CalculatePercentile(values, 0.25),
            Q3 = CalculatePercentile(values, 0.75),
            Count = values.Count,
            NullCount = allValues.Count - values.Count
        };
    }

    private static double CalculatePercentile(List<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
        {
            throw new ArgumentException("Cannot calculate percentile of empty list");
        }

        if (sortedValues.Count == 1)
        {
            return sortedValues[0];
        }

        // Calculate position (0-based index)
        var position = percentile * (sortedValues.Count - 1);
        var lowerIndex = (int)Math.Floor(position);
        var upperIndex = (int)Math.Ceiling(position);

        // If position is exactly on an index, return that value
        if (lowerIndex == upperIndex)
        {
            return sortedValues[lowerIndex];
        }

        // Linear interpolation between lower and upper values
        var lowerValue = sortedValues[lowerIndex];
        var upperValue = sortedValues[upperIndex];
        var fraction = position - lowerIndex;

        return lowerValue + fraction * (upperValue - lowerValue);
    }
}
