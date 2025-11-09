namespace FilePrepper.Tasks.DateTimeOps;

/// <summary>
/// Options for DateTime operations
/// </summary>
public class DateTimeOption : SingleInputOption
{
    /// <summary>
    /// Column to parse/transform
    /// </summary>
    public string Column { get; set; } = string.Empty;

    /// <summary>
    /// DateTime parsing mode
    /// </summary>
    public DateTimeMode Mode { get; set; }

    /// <summary>
    /// Input format for parsing (e.g., "yyyy-MM-dd HH:mm", "yyyyMMddHHmm")
    /// </summary>
    public string? InputFormat { get; set; }

    /// <summary>
    /// Output format (default: "yyyy-MM-dd HH:mm:ss")
    /// </summary>
    public string OutputFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    /// Date features to extract (comma-separated: Year,Month,Day,Hour,Minute,DayOfWeek,DayOfYear,WeekOfYear,Quarter)
    /// </summary>
    public string? Features { get; set; }

    /// <summary>
    /// Remove original column after feature extraction (default: false)
    /// </summary>
    public bool RemoveOriginal { get; set; } = false;

    protected override string[] ValidateInternal()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Column))
        {
            errors.Add("Column name is required");
        }

        if (Mode == DateTimeMode.Parse && string.IsNullOrWhiteSpace(InputFormat))
        {
            errors.Add("InputFormat is required for Parse mode");
        }

        if (Mode == DateTimeMode.ExtractFeatures && string.IsNullOrWhiteSpace(Features))
        {
            errors.Add("Features are required for ExtractFeatures mode");
        }

        return errors.ToArray();
    }
}

/// <summary>
/// DateTime operation modes
/// </summary>
public enum DateTimeMode
{
    /// <summary>
    /// Parse string to DateTime with custom format
    /// </summary>
    Parse,

    /// <summary>
    /// Parse Excel numeric date to DateTime
    /// </summary>
    ParseExcel,

    /// <summary>
    /// Extract date/time features from DateTime column
    /// </summary>
    ExtractFeatures
}
