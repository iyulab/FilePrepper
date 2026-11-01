namespace FilePrepper.Tasks.FilterRows;

/// <summary>How a cell is compared with a <see cref="FilterCondition.Value"/>.</summary>
public enum FilterOperator
{
    /// <summary>The cell is exactly the value (case-sensitive).</summary>
    Equals,

    /// <summary>The cell is not exactly the value (case-sensitive).</summary>
    NotEquals,

    /// <summary>Both are numbers and the cell is the larger; a cell or value that is not a number never matches.</summary>
    GreaterThan,

    /// <summary>Both are numbers and the cell is not the smaller; a cell or value that is not a number never matches.</summary>
    GreaterOrEqual,

    /// <summary>Both are numbers and the cell is the smaller; a cell or value that is not a number never matches.</summary>
    LessThan,

    /// <summary>Both are numbers and the cell is not the larger; a cell or value that is not a number never matches.</summary>
    LessOrEqual,

    /// <summary>The cell contains the value, ignoring case.</summary>
    Contains,

    /// <summary>The cell does not contain the value, ignoring case — exactly the records <see cref="Contains"/> leaves out.</summary>
    NotContains,

    /// <summary>The cell starts with the value, ignoring case.</summary>
    StartsWith,

    /// <summary>The cell ends with the value, ignoring case.</summary>
    EndsWith
}

/// <summary>One test of a record: the cell of a column against a value.</summary>
public class FilterCondition
{
    /// <summary>The column whose cell is tested. A record without this column does not match.</summary>
    public string ColumnName { get; set; } = string.Empty;

    /// <summary>How the cell is compared with <see cref="Value"/>.</summary>
    public FilterOperator Operator { get; set; }

    /// <summary>
    /// The value to compare with. The text operators compare ordinally and ignore case, the same on every machine;
    /// the number operators read both sides with a point as the decimal separator.
    /// </summary>
    public string Value { get; set; } = string.Empty;
}

/// <summary>Options of <see cref="FilterRowsTask"/>: the records that meet every condition are kept.</summary>
public class FilterRowsOption : SingleInputOption
{
    /// <summary>The conditions, all of which a record must meet (AND); at least one is required.</summary>
    public List<FilterCondition> Conditions { get; set; } = new();

    protected override string[] ValidateInternal()
    {
        var errors = new List<string>();

        if (Conditions.Count == 0)
        {
            errors.Add("Filter conditions must not be empty.");
        }
        else
        {
            foreach (var cond in Conditions)
            {
                if (string.IsNullOrWhiteSpace(cond.ColumnName))
                {
                    errors.Add("ColumnName in filter condition cannot be empty.");
                }
            }
        }

        return [.. errors];
    }
}