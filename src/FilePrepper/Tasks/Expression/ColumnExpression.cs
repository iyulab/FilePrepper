namespace FilePrepper.Tasks.Expression;

/// <summary>
/// One computed column: a name, an arithmetic expression over other columns, and where to put it. Written
/// <c>name=expression</c> or <c>name=expression@position</c>.
/// </summary>
public class ColumnExpression
{
    /// <summary>The name of the computed column; a column of that name already in the data is replaced.</summary>
    public string OutputColumnName { get; set; } = string.Empty;

    /// <summary>
    /// The expression (e.g. <c>col1 + col2</c>, <c>col1 * 100 - col2</c>): numbers, column names, <c>+ - * /</c> and parentheses.
    /// A column name that is not a column of the data is an error; a column that is empty or not a number in a record makes
    /// that record's result empty.
    /// </summary>
    public string Expression { get; set; } = string.Empty;

    /// <summary>
    /// Where the column goes among the columns that are there when its turn comes (expressions are placed in order): 0 is first,
    /// <c>n</c> is after the first n columns, -1 (the default) or a position past the end is last.
    /// </summary>
    public int InsertPosition { get; set; } = -1;

    /// <summary>Whether both a name and an expression were given.</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(OutputColumnName)
                          && !string.IsNullOrWhiteSpace(Expression);

    /// <summary>Reads <c>output_name=expression</c> or <c>output_name=expression@position</c>.</summary>
    /// <exception cref="ArgumentException">The text is empty, has no <c>=</c>, or has a position that is not a number.</exception>
    public static ColumnExpression Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Expression cannot be empty", nameof(value));
        }

        // Split by '@' first to check for position
        var atParts = value.Split('@');
        var mainPart = atParts[0];
        int position = -1;

        if (atParts.Length == 2)
        {
            if (!int.TryParse(atParts[1].Trim(), out position))
            {
                throw new ArgumentException($"Invalid position: {atParts[1]}", nameof(value));
            }
        }
        else if (atParts.Length > 2)
        {
            throw new ArgumentException("Expression format: 'output=expression' or 'output=expression@position'", nameof(value));
        }

        // Split by '=' to get output name and expression
        var equalsParts = mainPart.Split('=', 2);
        if (equalsParts.Length != 2)
        {
            throw new ArgumentException("Expression must contain '=' (format: 'output=expression')", nameof(value));
        }

        return new ColumnExpression
        {
            OutputColumnName = equalsParts[0].Trim(),
            Expression = equalsParts[1].Trim(),
            InsertPosition = position
        };
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var result = $"{OutputColumnName}={Expression}";
        if (InsertPosition >= 0)
        {
            result += $"@{InsertPosition}";
        }
        return result;
    }
}
