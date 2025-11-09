namespace FilePrepper.Tasks.Expression;

/// <summary>
/// Options for column expression task
/// </summary>
public class ExpressionOption : SingleInputOption
{
    /// <summary>
    /// List of column expressions to evaluate
    /// </summary>
    public List<ColumnExpression> Expressions { get; set; } = new();

    /// <summary>
    /// Whether to remove source columns used in expressions
    /// </summary>
    public bool RemoveSourceColumns { get; set; } = false;

    protected override string[] ValidateInternal()
    {
        var errors = new List<string>();

        if (Expressions == null || Expressions.Count == 0)
        {
            errors.Add("At least one expression must be specified.");
            return errors.ToArray();
        }

        foreach (var (expr, index) in Expressions.Select((e, i) => (e, i)))
        {
            if (!expr.IsValid)
            {
                errors.Add($"Expression at index {index} is invalid. Both OutputColumnName and Expression must be specified.");
            }

            // Check for duplicate output column names
            var duplicateCount = Expressions.Count(e => e.OutputColumnName == expr.OutputColumnName);
            if (duplicateCount > 1)
            {
                errors.Add($"Duplicate output column name: {expr.OutputColumnName}");
            }

            // Basic expression syntax validation
            if (string.IsNullOrWhiteSpace(expr.Expression))
            {
                errors.Add($"Expression at index {index} cannot be empty.");
            }
        }

        return errors.ToArray();
    }
}
