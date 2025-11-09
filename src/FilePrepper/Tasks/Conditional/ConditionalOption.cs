using FilePrepper.Tasks;

namespace FilePrepper.Tasks.Conditional;

public class ConditionalOption : SingleInputOption
{
    /// <summary>
    /// Name of the new column to create
    /// </summary>
    public string OutputColumn { get; set; } = string.Empty;

    /// <summary>
    /// List of condition-value pairs (if condition then value)
    /// Format: "Column operator Value : ResultValue"
    /// Example: "Price > 100 : High", "Status == Active : 1"
    /// </summary>
    public List<string> Conditions { get; set; } = new();

    /// <summary>
    /// Default value if no conditions match (else value)
    /// </summary>
    public string ElseValue { get; set; } = string.Empty;

    protected override string[] ValidateInternal()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(OutputColumn))
        {
            errors.Add("OutputColumn is required.");
        }

        if (Conditions == null || Conditions.Count == 0)
        {
            errors.Add("At least one condition is required.");
        }

        // Validate each condition format
        if (Conditions != null)
        {
            for (int i = 0; i < Conditions.Count; i++)
            {
                var condition = Conditions[i];
                if (!condition.Contains(':'))
                {
                    errors.Add($"Condition {i + 1} is invalid. Expected format: 'Column operator Value : ResultValue'");
                    continue;
                }

                var parts = condition.Split(':', 2);
                var condExpr = parts[0].Trim();

                // Check if condition has a valid operator
                var validOperators = new[] { "==", "!=", ">", "<", ">=", "<=", "contains", "startswith", "endswith" };
                if (!validOperators.Any(op => condExpr.Contains(op)))
                {
                    errors.Add($"Condition {i + 1} does not contain a valid operator. Valid: ==, !=, >, <, >=, <=, contains, startswith, endswith");
                }
            }
        }

        return errors.ToArray();
    }
}

/// <summary>
/// Represents a parsed conditional expression
/// </summary>
public class ConditionalExpression
{
    public string ColumnName { get; set; } = string.Empty;
    public ConditionalOperator Operator { get; set; }
    public string CompareValue { get; set; } = string.Empty;
    public string ResultValue { get; set; } = string.Empty;

    public static ConditionalExpression Parse(string conditionStr)
    {
        // Format: "Column operator Value : ResultValue"
        var parts = conditionStr.Split(':', 2);
        if (parts.Length != 2)
        {
            throw new ArgumentException($"Invalid condition format: {conditionStr}");
        }

        var condExpr = parts[0].Trim();
        var resultValue = parts[1].Trim();

        // Parse condition expression
        ConditionalOperator op;
        string columnName;
        string compareValue;

        if (condExpr.Contains("=="))
        {
            op = ConditionalOperator.Equals;
            var exprParts = condExpr.Split("==", 2);
            columnName = exprParts[0].Trim();
            compareValue = exprParts[1].Trim();
        }
        else if (condExpr.Contains("!="))
        {
            op = ConditionalOperator.NotEquals;
            var exprParts = condExpr.Split("!=", 2);
            columnName = exprParts[0].Trim();
            compareValue = exprParts[1].Trim();
        }
        else if (condExpr.Contains(">="))
        {
            op = ConditionalOperator.GreaterThanOrEqual;
            var exprParts = condExpr.Split(">=", 2);
            columnName = exprParts[0].Trim();
            compareValue = exprParts[1].Trim();
        }
        else if (condExpr.Contains("<="))
        {
            op = ConditionalOperator.LessThanOrEqual;
            var exprParts = condExpr.Split("<=", 2);
            columnName = exprParts[0].Trim();
            compareValue = exprParts[1].Trim();
        }
        else if (condExpr.Contains(">"))
        {
            op = ConditionalOperator.GreaterThan;
            var exprParts = condExpr.Split(">", 2);
            columnName = exprParts[0].Trim();
            compareValue = exprParts[1].Trim();
        }
        else if (condExpr.Contains("<"))
        {
            op = ConditionalOperator.LessThan;
            var exprParts = condExpr.Split("<", 2);
            columnName = exprParts[0].Trim();
            compareValue = exprParts[1].Trim();
        }
        else if (condExpr.Contains("contains"))
        {
            op = ConditionalOperator.Contains;
            var exprParts = condExpr.Split("contains", 2, StringSplitOptions.TrimEntries);
            columnName = exprParts[0].Trim();
            compareValue = exprParts[1].Trim();
        }
        else if (condExpr.Contains("startswith"))
        {
            op = ConditionalOperator.StartsWith;
            var exprParts = condExpr.Split("startswith", 2, StringSplitOptions.TrimEntries);
            columnName = exprParts[0].Trim();
            compareValue = exprParts[1].Trim();
        }
        else if (condExpr.Contains("endswith"))
        {
            op = ConditionalOperator.EndsWith;
            var exprParts = condExpr.Split("endswith", 2, StringSplitOptions.TrimEntries);
            columnName = exprParts[0].Trim();
            compareValue = exprParts[1].Trim();
        }
        else
        {
            throw new ArgumentException($"No valid operator found in condition: {condExpr}");
        }

        return new ConditionalExpression
        {
            ColumnName = columnName,
            Operator = op,
            CompareValue = compareValue,
            ResultValue = resultValue
        };
    }

    public bool Evaluate(string value)
    {
        switch (Operator)
        {
            case ConditionalOperator.Equals:
                return string.Equals(value, CompareValue, StringComparison.OrdinalIgnoreCase);

            case ConditionalOperator.NotEquals:
                return !string.Equals(value, CompareValue, StringComparison.OrdinalIgnoreCase);

            case ConditionalOperator.GreaterThan:
                if (double.TryParse(value, out var v1) && double.TryParse(CompareValue, out var c1))
                    return v1 > c1;
                return string.Compare(value, CompareValue, StringComparison.OrdinalIgnoreCase) > 0;

            case ConditionalOperator.LessThan:
                if (double.TryParse(value, out var v2) && double.TryParse(CompareValue, out var c2))
                    return v2 < c2;
                return string.Compare(value, CompareValue, StringComparison.OrdinalIgnoreCase) < 0;

            case ConditionalOperator.GreaterThanOrEqual:
                if (double.TryParse(value, out var v3) && double.TryParse(CompareValue, out var c3))
                    return v3 >= c3;
                return string.Compare(value, CompareValue, StringComparison.OrdinalIgnoreCase) >= 0;

            case ConditionalOperator.LessThanOrEqual:
                if (double.TryParse(value, out var v4) && double.TryParse(CompareValue, out var c4))
                    return v4 <= c4;
                return string.Compare(value, CompareValue, StringComparison.OrdinalIgnoreCase) <= 0;

            case ConditionalOperator.Contains:
                return value.Contains(CompareValue, StringComparison.OrdinalIgnoreCase);

            case ConditionalOperator.StartsWith:
                return value.StartsWith(CompareValue, StringComparison.OrdinalIgnoreCase);

            case ConditionalOperator.EndsWith:
                return value.EndsWith(CompareValue, StringComparison.OrdinalIgnoreCase);

            default:
                return false;
        }
    }
}

public enum ConditionalOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual,
    Contains,
    StartsWith,
    EndsWith
}
