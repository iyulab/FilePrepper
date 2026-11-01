namespace FilePrepper.Tasks.Expression;

/// <summary>Options of <see cref="ExpressionTask"/>: the computed columns to add.</summary>
public class ExpressionOption : SingleInputOption
{
    /// <summary>The columns to compute, each over the original columns of the record; at least one, with distinct names.</summary>
    public List<ColumnExpression> Expressions { get; set; } = new();

    /// <summary>Drop every column an expression reads from the output; false by default.</summary>
    public bool RemoveSourceColumns { get; set; } = false;

    /// <inheritdoc />
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
