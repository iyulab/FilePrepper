using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using FilePrepper.Utils;

namespace FilePrepper.Tasks.Expression;

/// <summary>
/// Evaluates an arithmetic expression over the cells of one record: numbers, column names, <c>+ - * /</c> and
/// parentheses, with the usual precedence and a unary minus.
/// </summary>
/// <remarks>
/// A column name is recognised wherever the record's columns say it is — the longest name that fits, not touching a
/// letter, digit or underscore on either side — so a name may hold spaces or punctuation (<c>unit price</c>,
/// <c>price($)</c>). A cell is read as a number the way every FilePrepper task reads one (<see cref="NumberText"/>: a
/// point for the decimal, an exponent allowed, no thousands separator); the expression is never rebuilt as text from the
/// cells, so a cell such as <c>0.00001</c> (<c>1E-05</c> as text) is the number it is. A cell that is empty or not a
/// number makes the whole result empty: a computation over a missing value is missing, not computed from a 0 nobody wrote.
/// </remarks>
public class ExpressionEvaluator
{
    private const char PlaceholderStart = '\u0001';
    private const char PlaceholderEnd = '\u0002';

    // One pattern per set of column names, built once however many records are evaluated.
    private static readonly ConditionalWeakTable<HashSet<string>, Regex> Patterns = new();

    private readonly Dictionary<string, string> _record;
    private readonly HashSet<string> _columnNames;

    /// <summary>Creates an evaluator for one record.</summary>
    /// <param name="record">The record's cells by column name.</param>
    /// <param name="columnNames">The names an expression may refer to; keep one set for every record of a file.</param>
    public ExpressionEvaluator(Dictionary<string, string> record, HashSet<string> columnNames)
    {
        _record = record;
        _columnNames = columnNames;
    }

    /// <summary>
    /// Evaluates an expression and returns the result as text (a point for the decimal), or an empty string when a column it
    /// reads is empty or not a number in this record.
    /// </summary>
    /// <exception cref="InvalidOperationException">The expression is malformed, names a column that does not exist, or divides by zero.</exception>
    public string Evaluate(string expression)
    {
        try
        {
            var text = Substitute(expression, out var values, out var missing);
            if (missing)
            {
                return string.Empty;
            }

            return NumberText.Format(EvaluateAddSubtract(Regex.Replace(text, @"\s+", ""), values));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to evaluate expression '{expression}': {ex.Message}", ex);
        }
    }

    /// <summary>The columns an expression reads.</summary>
    public HashSet<string> GetReferencedColumns(string expression)
    {
        var referenced = new HashSet<string>();
        if (_columnNames.Count == 0)
        {
            return referenced;
        }

        foreach (Match match in PatternFor(_columnNames).Matches(expression))
        {
            referenced.Add(match.Value);
        }

        return referenced;
    }

    /// <summary>
    /// The names in an expression that are neither a column nor a number — a column mistyped. Evaluating such an
    /// expression fails, so the task asks first and names them.
    /// </summary>
    public List<string> GetUnknownIdentifiers(string expression)
    {
        var rest = _columnNames.Count == 0 ? expression : PatternFor(_columnNames).Replace(expression, " ");
        var unknown = new List<string>();
        foreach (var token in Tokenize(rest))
        {
            if ((char.IsLetter(token[0]) || token[0] == '_') && !unknown.Contains(token))
            {
                unknown.Add(token);
            }
        }

        return unknown;
    }

    private static Regex PatternFor(HashSet<string> columnNames) =>
        Patterns.GetValue(columnNames, names =>
        {
            var alternatives = names
                .Where(n => n.Length > 0)
                .OrderByDescending(n => n.Length)
                .ThenBy(n => n, StringComparer.Ordinal)
                .Select(Regex.Escape);
            return new Regex($@"(?<![\p{{L}}\p{{N}}_])(?:{string.Join("|", alternatives)})(?![\p{{L}}\p{{N}}_])", RegexOptions.CultureInvariant);
        });

    // Replaces each column name with a placeholder that points at the cell's number, so that no number is written into the
    // expression as text (1E-05 would be read as 1E minus 05).
    private string Substitute(string expression, out List<double> values, out bool missing)
    {
        var cells = new List<double>();
        var anyMissing = false;

        var text = _columnNames.Count == 0
            ? expression
            : PatternFor(_columnNames).Replace(expression, match =>
            {
                if (_record.TryGetValue(match.Value, out var cell) && NumberText.TryParse(cell, out var number) && !double.IsNaN(number))
                {
                    cells.Add(number);
                    return $"{PlaceholderStart}{cells.Count - 1}{PlaceholderEnd}";
                }

                anyMissing = true;
                return "0";
            });

        values = cells;
        missing = anyMissing;
        return text;
    }

    private double EvaluateAddSubtract(string expression, List<double> values)
    {
        var parts = SplitByOperator(expression, ['+', '-']);
        double result = EvaluateMultiplyDivide(parts[0].value, values);
        for (int i = 1; i < parts.Count; i++)
        {
            var (op, value) = parts[i];
            var operand = EvaluateMultiplyDivide(value, values);
            result = op == '+' ? result + operand : result - operand;
        }

        return result;
    }

    private double EvaluateMultiplyDivide(string expression, List<double> values)
    {
        var parts = SplitByOperator(expression, ['*', '/']);
        double result = EvaluatePrimary(parts[0].value, values);
        for (int i = 1; i < parts.Count; i++)
        {
            var (op, value) = parts[i];
            var operand = EvaluatePrimary(value, values);
            if (op == '*')
            {
                result *= operand;
            }
            else
            {
                if (operand == 0)
                {
                    throw new DivideByZeroException("Division by zero in expression");
                }

                result /= operand;
            }
        }

        return result;
    }

    private double EvaluatePrimary(string expression, List<double> values)
    {
        expression = expression.Trim();

        if (expression.StartsWith('(') && expression.EndsWith(')') && Encloses(expression))
        {
            return EvaluateAddSubtract(expression[1..^1], values);
        }

        // A unary minus
        if (expression.StartsWith('-'))
        {
            return -EvaluatePrimary(expression[1..], values);
        }

        // A cell: its placeholder
        if (expression.Length > 2 && expression[0] == PlaceholderStart && expression[^1] == PlaceholderEnd
            && int.TryParse(expression.AsSpan(1, expression.Length - 2), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var index))
        {
            return values[index];
        }

        if (!NumberText.TryParse(expression, out var result))
        {
            throw new FormatException($"Invalid number: {expression}");
        }

        return result;
    }

    // Whether the opening parenthesis of '(a)*(b)' closes at the end: it does not.
    private static bool Encloses(string expression)
    {
        var depth = 0;
        for (var i = 0; i < expression.Length; i++)
        {
            if (expression[i] == '(')
            {
                depth++;
            }
            else if (expression[i] == ')' && --depth == 0)
            {
                return i == expression.Length - 1;
            }
        }

        return false;
    }

    private static List<(char op, string value)> SplitByOperator(string expression, char[] operators)
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
                // A sign is not an operator at the start, after another operator or an opening parenthesis, or inside
                // a number written with an exponent (1e-5).
                if ((c == '-' || c == '+') && (i == 0 || "+-*/(".Contains(expression[i - 1]) || IsExponentSign(expression, i)))
                {
                    continue;
                }

                parts.Add((lastOperator, expression[lastSplitIndex..i]));
                lastOperator = c;
                lastSplitIndex = i + 1;
            }
        }

        parts.Add((lastOperator, expression[lastSplitIndex..]));
        return parts;
    }

    // '1e-5', '2.5E+3': the sign follows an e that follows a digit or a point, and a digit follows the sign.
    private static bool IsExponentSign(string expression, int sign) =>
        sign >= 2 && sign + 1 < expression.Length
        && (expression[sign - 1] is 'e' or 'E')
        && (char.IsDigit(expression[sign - 2]) || expression[sign - 2] == '.')
        && char.IsDigit(expression[sign + 1])
        && StartsNumber(expression, sign - 2);

    // The characters before the exponent make up a number (so 'x1e-5', where x1e would be a name, is not one).
    private static bool StartsNumber(string expression, int last)
    {
        var i = last;
        while (i >= 0 && (char.IsDigit(expression[i]) || expression[i] == '.'))
        {
            i--;
        }

        return i < 0 || !(char.IsLetter(expression[i]) || expression[i] == '_' || expression[i] == PlaceholderEnd);
    }

    private static List<string> Tokenize(string expression)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();

        foreach (char c in expression)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                current.Append(c);
            }
            else if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }
}
