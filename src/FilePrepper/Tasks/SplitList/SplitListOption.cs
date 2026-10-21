namespace FilePrepper.Tasks.SplitList;

/// <summary>
/// Configuration for splitting list columns of one length into one column per position.
/// </summary>
public class SplitListOption : SingleInputOption
{
    /// <summary>The list columns to split; each becomes <c>name.0</c>, <c>name.1</c>, … in place.</summary>
    public List<string> Columns { get; set; } = [];

    protected override string[] ValidateInternal()
    {
        if (Columns == null || Columns.Count == 0)
            return ["At least one list column to split must be specified."];
        if (Columns.Any(string.IsNullOrWhiteSpace))
            return ["Column names to split cannot be empty or whitespace."];
        if (Columns.Distinct().Count() != Columns.Count)
            return ["A column to split is named more than once."];
        return [];
    }
}
