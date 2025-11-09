namespace FilePrepper.Tasks.Expression;

/// <summary>
/// Represents a column expression for computing new values from existing columns
/// </summary>
public class ColumnExpression
{
    /// <summary>
    /// Name of the new column to create
    /// </summary>
    public string OutputColumnName { get; set; } = string.Empty;

    /// <summary>
    /// Expression to evaluate (e.g., "col1 + col2", "col1 * 100 - col2")
    /// </summary>
    public string Expression { get; set; } = string.Empty;

    /// <summary>
    /// Position to insert the new column (0 = first, -1 = last)
    /// </summary>
    public int InsertPosition { get; set; } = -1;

    public bool IsValid => !string.IsNullOrWhiteSpace(OutputColumnName)
                          && !string.IsNullOrWhiteSpace(Expression);

    /// <summary>
    /// Parse expression from string format: "output_name=expression" or "output_name=expression@position"
    /// </summary>
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
