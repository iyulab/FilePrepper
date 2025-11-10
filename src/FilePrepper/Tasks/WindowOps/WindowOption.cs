namespace FilePrepper.Tasks.WindowOps;

/// <summary>
/// Types of window aggregation methods
/// </summary>
public enum AggregationMethod
{
    /// <summary>
    /// Calculate mean (average) value
    /// </summary>
    Mean,

    /// <summary>
    /// Calculate minimum value
    /// </summary>
    Min,

    /// <summary>
    /// Calculate maximum value
    /// </summary>
    Max,

    /// <summary>
    /// Calculate sum of values
    /// </summary>
    Sum,

    /// <summary>
    /// Count number of values
    /// </summary>
    Count,

    /// <summary>
    /// Calculate standard deviation
    /// </summary>
    Std,

    /// <summary>
    /// Calculate variance
    /// </summary>
    Var,

    /// <summary>
    /// Calculate median value
    /// </summary>
    Median,

    /// <summary>
    /// Get first value in group
    /// </summary>
    First,

    /// <summary>
    /// Get last value in group
    /// </summary>
    Last
}

/// <summary>
/// Types of window operations
/// </summary>
public enum WindowType
{
    /// <summary>
    /// Time-based resampling (e.g., 5-minute windows)
    /// </summary>
    Resample,

    /// <summary>
    /// Fixed-size rolling window
    /// </summary>
    Rolling
}

/// <summary>
/// Options for window operations (resample and rolling)
/// </summary>
public class WindowOption : BaseColumnOption
{
    /// <summary>
    /// Type of window operation
    /// </summary>
    public WindowType Type { get; set; } = WindowType.Resample;

    /// <summary>
    /// Aggregation method to apply
    /// </summary>
    public AggregationMethod Method { get; set; } = AggregationMethod.Mean;

    /// <summary>
    /// For Resample: time column name (e.g., "DateTime")
    /// </summary>
    public string? TimeColumn { get; set; }

    /// <summary>
    /// For Resample: window size (e.g., "5T" = 5 minutes, "1H" = 1 hour, "1D" = 1 day)
    /// Format: [number][T|H|D] where T=minutes, H=hours, D=days
    /// </summary>
    public string? Window { get; set; }

    /// <summary>
    /// For Rolling: window size in number of rows
    /// </summary>
    public int WindowSize { get; set; } = 3;

    /// <summary>
    /// For Rolling: suffix to add to output column names
    /// </summary>
    public string OutputSuffix { get; set; } = "_rolling";

    protected override string[] ValidateInternal()
    {
        var errors = new List<string>();

        if (TargetColumns == null || TargetColumns.Length == 0)
        {
            errors.Add("At least one target column must be specified for window operations.");
        }

        if (Type == WindowType.Resample)
        {
            if (string.IsNullOrWhiteSpace(TimeColumn))
            {
                errors.Add("TimeColumn must be specified for Resample operation.");
            }

            if (string.IsNullOrWhiteSpace(Window))
            {
                errors.Add("Window must be specified for Resample operation (e.g., '5T', '1H', '1D').");
            }
            else
            {
                // Validate window format
                if (!System.Text.RegularExpressions.Regex.IsMatch(Window, @"^\d+[THD]$"))
                {
                    errors.Add("Window format is invalid. Use format like '5T' (5 minutes), '1H' (1 hour), or '1D' (1 day).");
                }
            }
        }
        else if (Type == WindowType.Rolling)
        {
            if (WindowSize < 1)
            {
                errors.Add("WindowSize must be at least 1 for Rolling operation.");
            }

            if (string.IsNullOrWhiteSpace(OutputSuffix))
            {
                errors.Add("OutputSuffix cannot be empty for Rolling operation.");
            }
        }

        return [.. errors];
    }
}
