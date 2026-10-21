namespace FilePrepper.Tasks.Explode;

/// <summary>
/// Configuration for exploding list columns into one row per element.
/// </summary>
public class ExplodeOption : SingleInputOption
{
    /// <summary>
    /// The list columns to explode together. Their lists are paired by position, so in each row
    /// they must have the same length.
    /// </summary>
    public List<string> Columns { get; set; } = [];

    protected override string[] ValidateInternal()
    {
        if (Columns == null || Columns.Count == 0)
            return ["At least one column to explode must be specified."];
        if (Columns.Any(string.IsNullOrWhiteSpace))
            return ["Column names to explode cannot be empty or whitespace."];
        if (Columns.Distinct().Count() != Columns.Count)
            return ["A column to explode is named more than once."];
        return [];
    }
}
