using Microsoft.Extensions.Logging;

namespace FilePrepper.Tasks.Expression;

/// <summary>
/// Task to create computed columns from expressions
/// </summary>
public class ExpressionTask : BaseTask<ExpressionOption>
{
    public ExpressionTask(ILogger<ExpressionTask> logger) : base(logger)
    {
    }

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

        // Validate all expressions reference valid columns
        foreach (var expr in Options.Expressions)
        {
            var evaluator = new ExpressionEvaluator(new Dictionary<string, string>(), columnSet);
            var referencedColumns = evaluator.GetReferencedColumns(expr.Expression);

            var missingColumns = referencedColumns.Except(columnSet).ToList();
            if (missingColumns.Any())
            {
                throw new InvalidOperationException(
                    $"Expression '{expr.Expression}' references non-existent columns: {string.Join(", ", missingColumns)}");
            }

            _logger.LogInformation("  Expression: {Expression}", expr.ToString());
        }

        // Process records - simplified approach
        var processedRecords = records.Select(record =>
        {
            var evaluator = new ExpressionEvaluator(record, columnSet);
            var newRecord = new Dictionary<string, string>();

            // Determine which source columns to remove
            var columnsToRemove = new HashSet<string>();
            if (Options.RemoveSourceColumns)
            {
                foreach (var expr in Options.Expressions)
                {
                    var referenced = evaluator.GetReferencedColumns(expr.Expression);
                    foreach (var col in referenced)
                    {
                        columnsToRemove.Add(col);
                    }
                }
            }

            // Copy all source columns (except removed ones)
            foreach (var sourceColumn in _originalHeaders)
            {
                if (!columnsToRemove.Contains(sourceColumn))
                {
                    newRecord[sourceColumn] = record[sourceColumn];
                }
            }

            // Add all computed columns at the end
            foreach (var expr in Options.Expressions)
            {
                var result = evaluator.Evaluate(expr.Expression);
                newRecord[expr.OutputColumnName] = result;
            }

            return newRecord;
        }).ToList();

        _logger.LogInformation("Expression evaluation completed successfully");
        _logger.LogInformation("Created {Count} new columns", Options.Expressions.Count);

        return Task.FromResult(processedRecords);
    }

    private List<string> CreateNewHeaders(List<string> sourceHeaders, ExpressionOption options)
    {
        var result = new List<string>();
        var columnSet = new HashSet<string>(sourceHeaders);

        // Determine which columns to remove
        var columnsToRemove = new HashSet<string>();
        if (options.RemoveSourceColumns)
        {
            var evaluator = new ExpressionEvaluator(new Dictionary<string, string>(), columnSet);
            foreach (var expr in options.Expressions)
            {
                var referenced = evaluator.GetReferencedColumns(expr.Expression);
                foreach (var col in referenced)
                {
                    columnsToRemove.Add(col);
                }
            }
        }

        // Add source columns (except removed ones)
        foreach (var sourceColumn in sourceHeaders)
        {
            if (!columnsToRemove.Contains(sourceColumn))
            {
                result.Add(sourceColumn);
            }
        }

        // Add all expression columns at the end
        foreach (var expr in options.Expressions)
        {
            result.Add(expr.OutputColumnName);
        }

        return result;
    }
}
