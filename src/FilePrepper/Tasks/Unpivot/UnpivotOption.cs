namespace FilePrepper.Tasks.Unpivot;

/// <summary>
/// Represents a group of columns to unpivot together
/// Example: For Dataset 006, each group would be a pair like ("1차 출고날짜", "1차 출고량")
/// </summary>
public class ColumnPairGroup
{
    /// <summary>
    /// List of column names that form a group (e.g., ["1차 출고날짜", "1차 출고량"])
    /// </summary>
    public List<string> Columns { get; set; } = [];

    /// <summary>
    /// Optional index value for this group (e.g., "1" for "1차" group)
    /// If not specified, will use sequential numbering starting from 1
    /// </summary>
    public string? IndexValue { get; set; }
}

/// <summary>
/// Configuration for unpivot (wide-to-long) transformation
/// </summary>
public class UnpivotOption : SingleInputOption
{
    /// <summary>
    /// Base columns to keep in every output row (e.g., ["작업지시번호", "도면", "품명"])
    /// These columns will be repeated for each unpivoted row
    /// </summary>
    public List<string> BaseColumns { get; set; } = [];

    /// <summary>
    /// Column groups to unpivot
    /// Each group represents columns that should be unpivoted together
    /// Example: For 10 shipment pairs, this would have 10 groups
    /// </summary>
    public List<ColumnPairGroup> ColumnGroups { get; set; } = [];

    /// <summary>
    /// Name for the index column in output (e.g., "출고차수")
    /// This column will contain the index values from ColumnPairGroup
    /// </summary>
    public string IndexColumnName { get; set; } = "Index";

    /// <summary>
    /// Names for the value columns in output (e.g., ["출고날짜", "출고량"])
    /// Must match the number of columns in each ColumnPairGroup
    /// </summary>
    public List<string> ValueColumnNames { get; set; } = [];

    /// <summary>
    /// Whether to skip rows where all value columns are empty
    /// Default: true
    /// </summary>
    public bool SkipEmptyRows { get; set; } = true;

    protected override string[] ValidateInternal()
    {
        var errors = new List<string>();

        if (ColumnGroups == null || ColumnGroups.Count == 0)
        {
            errors.Add("At least one column group must be specified for unpivot");
            return [.. errors];
        }

        if (ValueColumnNames == null || ValueColumnNames.Count == 0)
        {
            errors.Add("Value column names must be specified");
            return [.. errors];
        }

        // Validate all column groups have the same number of columns
        var firstGroupColumnCount = ColumnGroups[0].Columns.Count;
        if (ColumnGroups.Any(g => g.Columns.Count != firstGroupColumnCount))
        {
            errors.Add("All column groups must have the same number of columns");
        }

        // Validate value column names match the number of columns in each group
        if (ValueColumnNames.Count != firstGroupColumnCount)
        {
            errors.Add($"Number of value column names ({ValueColumnNames.Count}) must match number of columns in each group ({firstGroupColumnCount})");
        }

        // Validate column names are not empty
        foreach (var group in ColumnGroups)
        {
            if (group.Columns.Any(string.IsNullOrWhiteSpace))
            {
                errors.Add("Column names in groups cannot be empty or whitespace");
            }
        }

        if (string.IsNullOrWhiteSpace(IndexColumnName))
        {
            errors.Add("Index column name cannot be empty or whitespace");
        }

        return [.. errors];
    }
}
