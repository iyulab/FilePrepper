using System.Globalization;
using System.Text.RegularExpressions;

namespace FilePrepper.Tasks.Expression;

/// <summary>
/// Simple expression evaluator for arithmetic operations with column references
/// </summary>
public class ExpressionEvaluator
{
    private readonly Dictionary<string, string> _record;
    private readonly HashSet<string> _columnNames;

    public ExpressionEvaluator(Dictionary<string, string> record, HashSet<string> columnNames)
    {
        _record = record;
        _columnNames = columnNames;
    }

    /// <summary>
    /// Evaluate an expression and return the result as a string
    /// </summary>
    public string Evaluate(string expression)
    {
        try
        {
            // Replace column names with their values
            var processedExpression = SubstituteColumnValues(expression);

            // Evaluate the arithmetic expression
            var result = EvaluateArithmetic(processedExpression);

            return result.ToString(CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to evaluate expression '{expression}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Get all column names referenced in the expression
    /// </summary>
    public HashSet<string> GetReferencedColumns(string expression)
    {
        var referenced = new HashSet<string>();

        // Match column names (identifiers that are not numbers or operators)
        var tokens = Tokenize(expression);
        foreach (var token in tokens)
        {
            if (_columnNames.Contains(token))
            {
                referenced.Add(token);
            }
        }

        return referenced;
    }

    private string SubstituteColumnValues(string expression)
    {
        var result = expression;

        // Sort column names by length (descending) to handle substring matches correctly
        var sortedColumns = _columnNames.OrderByDescending(c => c.Length).ToList();

        foreach (var columnName in sortedColumns)
        {
            // Use word boundary regex to match whole column names only
            var pattern = $@"\b{Regex.Escape(columnName)}\b";
            var value = _record.GetValueOrDefault(columnName, "0");

            // Parse value as double, default to 0 if invalid
            if (!double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var numValue))
            {
                numValue = 0;
            }

            result = Regex.Replace(result, pattern, numValue.ToString(CultureInfo.InvariantCulture));
        }

        return result;
    }

    private double EvaluateArithmetic(string expression)
    {
        // Remove all whitespace
        expression = Regex.Replace(expression, @"\s+", "");

        return EvaluateAddSubtract(expression);
    }

    private double EvaluateAddSubtract(string expression)
    {
        var parts = SplitByOperator(expression, new[] { '+', '-' });
        if (parts.Count == 1)
        {
            return EvaluateMultiplyDivide(parts[0].value);
        }

        double result = EvaluateMultiplyDivide(parts[0].value);
        for (int i = 1; i < parts.Count; i++)
        {
            var (op, value) = parts[i];
            var operand = EvaluateMultiplyDivide(value);

            if (op == '+')
                result += operand;
            else if (op == '-')
                result -= operand;
        }

        return result;
    }

    private double EvaluateMultiplyDivide(string expression)
    {
        var parts = SplitByOperator(expression, new[] { '*', '/' });
        if (parts.Count == 1)
        {
            return EvaluatePrimary(parts[0].value);
        }

        double result = EvaluatePrimary(parts[0].value);
        for (int i = 1; i < parts.Count; i++)
        {
            var (op, value) = parts[i];
            var operand = EvaluatePrimary(value);

            if (op == '*')
                result *= operand;
            else if (op == '/')
            {
                if (Math.Abs(operand) < 1e-10)
                    throw new DivideByZeroException("Division by zero in expression");
                result /= operand;
            }
        }

        return result;
    }

    private double EvaluatePrimary(string expression)
    {
        expression = expression.Trim();

        // Handle parentheses
        if (expression.StartsWith("(") && expression.EndsWith(")"))
        {
            return EvaluateAddSubtract(expression.Substring(1, expression.Length - 2));
        }

        // Handle unary minus
        if (expression.StartsWith("-"))
        {
            return -EvaluatePrimary(expression.Substring(1));
        }

        // Parse as number
        if (!double.TryParse(expression, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
        {
            throw new FormatException($"Invalid number: {expression}");
        }

        return result;
    }

    private List<(char op, string value)> SplitByOperator(string expression, char[] operators)
    {
        var parts = new List<(char op, string value)>();
        int parenthesesDepth = 0;
        int lastSplitIndex = 0;
        char lastOperator = '\0';

        for (int i = 0; i < expression.Length; i++)
        {
            char c = expression[i];

            if (c == '(')
            {
                parenthesesDepth++;
            }
            else if (c == ')')
            {
                parenthesesDepth--;
            }
            else if (parenthesesDepth == 0 && operators.Contains(c))
            {
                // Skip if it's a unary minus (at start or after another operator)
                if (c == '-' && (i == 0 || operators.Contains(expression[i - 1]) || expression[i - 1] == '('))
                {
                    continue;
                }

                var part = expression.Substring(lastSplitIndex, i - lastSplitIndex);
                parts.Add((lastOperator, part));
                lastOperator = c;
                lastSplitIndex = i + 1;
            }
        }

        // Add the last part
        parts.Add((lastOperator, expression.Substring(lastSplitIndex)));

        return parts;
    }

    private List<string> Tokenize(string expression)
    {
        var tokens = new List<string>();
        var currentToken = "";

        foreach (char c in expression)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                currentToken += c;
            }
            else
            {
                if (currentToken.Length > 0)
                {
                    tokens.Add(currentToken);
                    currentToken = "";
                }
            }
        }

        if (currentToken.Length > 0)
        {
            tokens.Add(currentToken);
        }

        return tokens;
    }
}
