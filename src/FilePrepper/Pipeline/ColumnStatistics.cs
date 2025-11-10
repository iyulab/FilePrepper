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
}
