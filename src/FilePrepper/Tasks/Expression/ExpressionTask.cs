using Microsoft.Extensions.Logging;

namespace FilePrepper.Tasks.Expression;

/// <summary>
/// Adds computed columns: each <see cref="ColumnExpression"/> of <see cref="ExpressionOption"/> is evaluated over every record
/// (see <see cref="ExpressionEvaluator"/> for the arithmetic and for what happens to an empty cell).
/// </summary>
public class ExpressionTask : BaseTask<ExpressionOption>
{
    /// <summary>Creates the task.</summary>
    /// <param name="logger">Where progress is logged.</param>
    public ExpressionTask(ILogger<ExpressionTask> logger) : base(logger)
    {
    }

    /// <inheritdoc />
    protected override Task<List<Dictionary<string, string>>> ProcessRecordsAsync(
        List<Dictionary<string, string>> records)
    {
        _logger.LogInformation("Processing {Count} records with {ExprCount} expression(s)",
            records.Count, Options.Expressions.Count);

        if (records.Count == 0)
        {
            _logger.LogWarning("No records to process");
            return Task.FromResult(records);
        }

        var columnSet = new HashSet<string>(_originalHeaders);
        var columnsToRemove = new HashSet<string>();

        // Every name in an expression must be a column; a mistyped one is named here, not found per record.
        foreach (var expr in Options.Expressions)
        {
            var checker = new ExpressionEvaluator(new Dictionary<string, string>(), columnSet);
            var unknown = checker.GetUnknownIdentifiers(expr.Expression);
            if (unknown.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Expression '{expr.Expression}' references non-existent columns: {string.Join(", ", unknown)}");
            }

            if (Options.RemoveSourceColumns)
            {
                columnsToRemove.UnionWith(checker.GetReferencedColumns(expr.Expression));
            }

            _logger.LogInformation("  Expression: {Expression}", expr.ToString());
        }

        var columnOrder = ColumnOrder(columnsToRemove);

        var emptyResults = 0;
        var processedRecords = records.Select(record =>
        {
            var evaluator = new ExpressionEvaluator(record, columnSet);
            var results = new Dictionary<string, string>();
            foreach (var expr in Options.Expressions)
            {
                var result = evaluator.Evaluate(expr.Expression);
                if (result.Length == 0)
                {
                    emptyResults++;
                }

                results[expr.OutputColumnName] = result;
            }

            var newRecord = new Dictionary<string, string>();
            foreach (var column in columnOrder)
            {
                newRecord[column] = results.TryGetValue(column, out var computed) ? computed : record[column];
            }

            return newRecord;
        }).ToList();

        if (emptyResults > 0)
        {
            _logger.LogWarning("{Count} result cell(s) are empty: a column the expression reads was empty or not a number", emptyResults);
        }

        _logger.LogInformation("Expression evaluation completed successfully");
        _logger.LogInformation("Created {Count} new columns", Options.Expressions.Count);

        return Task.FromResult(processedRecords);
    }

    // The source columns (less the removed ones), with each computed column put at its InsertPosition in turn: 0 first, n
    // after n columns, -1 (or a position past the end) last. A computed column named like a source column replaces it.
    private List<string> ColumnOrder(HashSet<string> columnsToRemove)
    {
        var order = _originalHeaders.Where(c => !columnsToRemove.Contains(c)).ToList();
        foreach (var expr in Options.Expressions)
        {
            order.Remove(expr.OutputColumnName);
            var at = expr.InsertPosition < 0 ? order.Count : Math.Min(expr.InsertPosition, order.Count);
            order.Insert(at, expr.OutputColumnName);
        }

        return order;
    }
}
