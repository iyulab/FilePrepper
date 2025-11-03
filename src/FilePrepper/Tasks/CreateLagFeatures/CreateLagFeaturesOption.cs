namespace FilePrepper.Tasks.CreateLagFeatures;

/// <summary>
/// Options for creating lag features from time series data
/// </summary>
public class CreateLagFeaturesOption : SingleInputOption
{
    /// <summary>
    /// Column to group by (e.g., Part Number, Entity ID)
    /// </summary>
    public string GroupByColumn { get; set; } = string.Empty;

    /// <summary>
    /// Column representing time/sequence (for sorting within groups)
    /// </summary>
    public string TimeColumn { get; set; } = string.Empty;

    /// <summary>
    /// Columns to create lag features from (e.g., D+3, D+4, D+5 planned quantities)
    /// </summary>
    public List<string> LagColumns { get; set; } = [];

    /// <summary>
    /// Lag periods to create (e.g., [1, 2, 3] creates lag-1, lag-2, lag-3 features)
    /// </summary>
    public List<int> LagPeriods { get; set; } = [];

    /// <summary>
    /// Target column to predict (optional, will be kept in output)
    /// </summary>
    public string? TargetColumn { get; set; }

    /// <summary>
    /// Drop rows with null lag values (default: true)
    /// </summary>
    public bool DropNullRows { get; set; } = true;

    /// <summary>
    /// Additional columns to keep in output (beyond group, lags, and target)
    /// </summary>
    public List<string> KeepColumns { get; set; } = [];

    protected override string[] ValidateInternal()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(GroupByColumn))
        {
            errors.Add("GroupByColumn is required");
        }

        if (string.IsNullOrWhiteSpace(TimeColumn))
        {
            errors.Add("TimeColumn is required");
        }

        if (LagColumns == null || LagColumns.Count == 0)
        {
            errors.Add("At least one LagColumn is required");
        }

        if (LagPeriods == null || LagPeriods.Count == 0)
        {
            errors.Add("At least one LagPeriod is required");
        }

        if (LagPeriods != null && LagPeriods.Any(p => p <= 0))
        {
            errors.Add("All LagPeriods must be positive integers");
        }

        return [.. errors];
    }
}
