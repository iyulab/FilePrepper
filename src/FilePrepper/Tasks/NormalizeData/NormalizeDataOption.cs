namespace FilePrepper.Tasks.NormalizeData;

/// <summary>
/// Types of normalization methods
/// </summary>
public enum NormalizationMethod
{
    /// <summary>
    /// Min-Max scaling: (x - min) / (max - min)
    /// Scales data to range [0, 1] or custom range
    /// </summary>
    MinMax,

    /// <summary>
    /// Z-score standardization: (x - mean) / std
    /// Centers data to mean=0, std=1
    /// </summary>
    ZScore,

    /// <summary>
    /// Robust normalization: (x - median) / IQR
    /// Robust to outliers, centers to median=0
    /// </summary>
    Robust
}

/// <summary>
/// Options for data normalization (e.g., which columns to normalize, which method, etc.)
/// </summary>
public class NormalizeDataOption : BaseColumnOption, IDefaultValueOption
{
    public NormalizationMethod Method { get; set; } = NormalizationMethod.MinMax;
    public double MinValue { get; set; } = 0.0;
    public double MaxValue { get; set; } = 1.0;

    // IDefaultValueOption implementation
    public string? DefaultValue { get; set; }

    protected override string[] ValidateInternal()
    {
        var errors = new List<string>();

        if (TargetColumns == null || TargetColumns.Length == 0)
        {
            errors.Add("At least one target column must be specified for normalization.");
        }

        // If using MinMax, ensure MinValue < MaxValue
        if (Method == NormalizationMethod.MinMax)
        {
            if (MinValue >= MaxValue)
            {
                errors.Add("MinValue must be less than MaxValue for MinMax normalization.");
            }
        }

        return [.. errors];
    }
}