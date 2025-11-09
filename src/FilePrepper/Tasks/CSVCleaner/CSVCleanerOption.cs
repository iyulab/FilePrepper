namespace FilePrepper.Tasks.CSVCleaner;

/// <summary>
/// Options for CSV cleaning task
/// </summary>
public class CSVCleanerOption : SingleInputOption
{
    /// <summary>
    /// Columns to clean (remove thousand separators and fix numeric format)
    /// If empty, all columns will be processed
    /// </summary>
    public List<string> TargetColumns { get; set; } = new();

    /// <summary>
    /// Character used as thousand separator (default: comma)
    /// </summary>
    public char ThousandSeparator { get; set; } = ',';

    /// <summary>
    /// Whether to remove all whitespace from numeric values
    /// </summary>
    public bool RemoveWhitespace { get; set; } = true;

    /// <summary>
    /// Whether to validate that cleaned values are valid numbers
    /// </summary>
    public bool ValidateNumeric { get; set; } = false;

    protected override string[] ValidateInternal()
    {
        // CSV Cleaner has no strict validation requirements
        // All options have defaults and empty target columns means "all columns"
        return Array.Empty<string>();
    }
}
