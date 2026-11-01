using FilePrepper.Utils;
using FilePrepper.Tasks;

namespace FilePrepper.Tasks.Conditional;

/// <summary>
/// Options of <see cref="ConditionalTask"/>: a new column whose value is the result of the first condition a record
/// meets, or <see cref="ElseValue"/> when it meets none.
/// </summary>
public class ConditionalOption : SingleInputOption
{
    /// <summary>
    /// The name of the new column; required. A column of that name already in the data is overwritten.
    /// </summary>
    public string OutputColumn { get; set; } = string.Empty;

    /// <summary>
    /// The conditions, tried in order; the first one a record meets gives its result (at least one is required).
    /// Each is <c>Column operator Value : ResultValue</c>, e.g. <c>Price &gt; 100 : High</c> or <c>Status == Active : 1</c>.
    /// The operators are <c>==</c> <c>!=</c> <c>&gt;</c> <c>&lt;</c> <c>&gt;=</c> <c>&lt;=</c> and the lower-case words
    /// <c>contains</c> <c>startswith</c> <c>endswith</c>. The condition and the result are separated by a colon
    /// with spaces around it (<c> : </c>) when the line has one, so a value may hold a colon
    /// (<c>Time == 12:30 : Peak</c>); a line without one is split at its first colon (<c>Price&gt;100:High</c>).
    /// Comparisons: <c>==</c> and <c>!=</c> and the three word operators ignore case; <c>&gt;</c> <c>&lt;</c> <c>&gt;=</c> <c>&lt;=</c>
    /// compare numbers when the value is a number, in which case a cell that is empty or not a number meets none of them,
    /// and compare text (ordinally, ignoring case) when the value is not a number.
    /// </summary>
    public List<string> Conditions { get; set; } = new();

    /// <summary>The result for a record that meets no condition; empty by default.</summary>
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

                var condExpr = ConditionalExpression.SplitCondition(condition)!.Value.Condition.Trim();

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
/// One parsed condition of <see cref="ConditionalOption.Conditions"/>: <c>Column operator Value : ResultValue</c>.
/// </summary>
public class ConditionalExpression
{
    /// <summary>The column whose cell is tested.</summary>
    public string ColumnName { get; set; } = string.Empty;

    /// <summary>How the cell is compared with <see cref="CompareValue"/>.</summary>
    public ConditionalOperator Operator { get; set; }

    /// <summary>The value the cell is compared with.</summary>
    public string CompareValue { get; set; } = string.Empty;

    /// <summary>What the new column holds for a record that meets the condition.</summary>
    public string ResultValue { get; set; } = string.Empty;

    /// <summary>
    /// Splits <c>condition : result</c> into its two halves: at the first <c> : </c> (a colon with spaces around it) when
    /// the line has one — the compared value may then hold colons, as a time does — otherwise at its first colon.
    /// Null when the line has no colon.
    /// </summary>
    public static (string Condition, string Result)? SplitCondition(string conditionStr)
    {
        ArgumentNullException.ThrowIfNull(conditionStr);
        var at = conditionStr.IndexOf(" : ", StringComparison.Ordinal);
        if (at >= 0)
            return (conditionStr[..at], conditionStr[(at + 3)..]);

        at = conditionStr.IndexOf(':', StringComparison.Ordinal);
        return at < 0 ? null : (conditionStr[..at], conditionStr[(at + 1)..]);
    }

    /// <summary>Reads <c>Column operator Value : ResultValue</c>.</summary>
    /// <exception cref="ArgumentException">The line has no colon, or no operator.</exception>
    public static ConditionalExpression Parse(string conditionStr)
    {
        // Format: "Column operator Value : ResultValue"
        var split = SplitCondition(conditionStr)
            ?? throw new ArgumentException($"Invalid condition format: {conditionStr}");

        var condExpr = split.Condition.Trim();
        var resultValue = split.Result.Trim();

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

    /// <summary>Whether a cell meets the condition; see <see cref="ConditionalOption.Conditions"/> for how each operator compares.</summary>
    /// <param name="value">The cell's text.</param>
    public bool Evaluate(string value)
    {
        switch (Operator)
        {
            case ConditionalOperator.Equals:
                return string.Equals(value, CompareValue, StringComparison.OrdinalIgnoreCase);

            case ConditionalOperator.NotEquals:
                return !string.Equals(value, CompareValue, StringComparison.OrdinalIgnoreCase);

            case ConditionalOperator.GreaterThan:
                return Order(value) is { } gt && gt > 0;

            case ConditionalOperator.LessThan:
                return Order(value) is { } lt && lt < 0;

            case ConditionalOperator.GreaterThanOrEqual:
                return Order(value) is { } ge && ge >= 0;

            case ConditionalOperator.LessThanOrEqual:
                return Order(value) is { } le && le <= 0;

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

    // The cell's place against the value: negative, zero or positive; null when there is none. A value that is a number is
    // compared with numbers, and a cell that is empty or not a number has no place against it — it used to be compared as
    // text, so 'abc' was greater than 100 and an empty cell was less than 100. A value that is not a number (a date, a word)
    // is compared as text.
    private int? Order(string value)
    {
        if (NumberText.TryParse(CompareValue, out var compare))
        {
            if (!NumberText.TryParse(value, out var cell) || double.IsNaN(cell) || double.IsNaN(compare))
                return null;
            return cell > compare ? 1 : cell < compare ? -1 : 0;
        }

        return string.Compare(value, CompareValue, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>How <see cref="ConditionalExpression"/> compares a cell with its value.</summary>
public enum ConditionalOperator
{
    /// <summary><c>==</c>: the same text, ignoring case.</summary>
    Equals,

    /// <summary><c>!=</c>: not the same text, ignoring case.</summary>
    NotEquals,

    /// <summary><c>&gt;</c>: greater, as numbers if the value is a number, else as text.</summary>
    GreaterThan,

    /// <summary><c>&lt;</c>: less, as numbers if the value is a number, else as text.</summary>
    LessThan,

    /// <summary><c>&gt;=</c>: not less, as numbers if the value is a number, else as text.</summary>
    GreaterThanOrEqual,

    /// <summary><c>&lt;=</c>: not greater, as numbers if the value is a number, else as text.</summary>
    LessThanOrEqual,

    /// <summary><c>contains</c>: the cell holds the value, ignoring case.</summary>
    Contains,

    /// <summary><c>startswith</c>: the cell starts with the value, ignoring case.</summary>
    StartsWith,

    /// <summary><c>endswith</c>: the cell ends with the value, ignoring case.</summary>
    EndsWith
}
