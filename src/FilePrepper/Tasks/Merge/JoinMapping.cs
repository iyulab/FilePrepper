namespace FilePrepper.Tasks.Merge;

/// <summary>
/// Represents a mapping between columns in different files for join operations.
/// Allows joining on columns with different names.
/// </summary>
public class JoinMapping
{
    /// <summary>
    /// Column name or index in the left (primary) file
    /// </summary>
    public ColumnIdentifier LeftColumn { get; set; } = new();

    /// <summary>
    /// Column name or index in the right (secondary) file
    /// </summary>
    public ColumnIdentifier RightColumn { get; set; } = new();

    /// <summary>
    /// Optional: Name for the join column in the output.
    /// If not specified, uses the left column name.
    /// </summary>
    public string? OutputColumnName { get; set; }

    /// <summary>
    /// Validates that both left and right columns are properly specified
    /// </summary>
    public bool IsValid => LeftColumn.IsValid && RightColumn.IsValid;

    /// <summary>
    /// Creates a JoinMapping from a string format "left:right" or "left:right:output"
    /// </summary>
    public static JoinMapping Parse(string mapping)
    {
        var parts = mapping.Split(':');

        if (parts.Length < 2 || parts.Length > 3)
        {
            throw new ArgumentException(
                $"Invalid join mapping format: '{mapping}'. " +
                "Expected format: 'leftColumn:rightColumn' or 'leftColumn:rightColumn:outputName'");
        }

        var result = new JoinMapping
        {
            LeftColumn = ColumnIdentifier.Parse(parts[0].Trim()),
            RightColumn = ColumnIdentifier.Parse(parts[1].Trim())
        };

        if (parts.Length == 3)
        {
            result.OutputColumnName = parts[2].Trim();
        }

        return result;
    }

    public override string ToString()
    {
        var result = $"{LeftColumn}:{RightColumn}";
        if (!string.IsNullOrEmpty(OutputColumnName))
        {
            result += $":{OutputColumnName}";
        }
        return result;
    }
}
