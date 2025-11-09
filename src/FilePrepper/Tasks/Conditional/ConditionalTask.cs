namespace FilePrepper.Tasks.Conditional;

public class ConditionalTask : BaseTask<ConditionalOption>
{
    private List<ConditionalExpression> _parsedConditions = new();

    public ConditionalTask(ILogger<ConditionalTask> logger) : base(logger) { }

    protected override async Task<List<Dictionary<string, string>>> ProcessRecordsAsync(
        List<Dictionary<string, string>> records)
    {
        _logger.LogInformation("Starting Conditional column creation");
        _logger.LogInformation("  Output column: {Column}", Options.OutputColumn);
        _logger.LogInformation("  Conditions: {Count}", Options.Conditions.Count);

        // Parse all conditions
        try
        {
            _parsedConditions = Options.Conditions
                .Select(ConditionalExpression.Parse)
                .ToList();

            _logger.LogInformation("Parsed {Count} conditional expressions", _parsedConditions.Count);
        }
        catch (Exception ex)
        {
            throw new ValidationException(
                $"Failed to parse conditions: {ex.Message}",
                ValidationExceptionErrorCode.General);
        }

        // Validate that all referenced columns exist
        var firstRecord = records.FirstOrDefault();
        if (firstRecord != null)
        {
            foreach (var condition in _parsedConditions)
            {
                if (!firstRecord.ContainsKey(condition.ColumnName))
                {
                    throw new ValidationException(
                        $"Column '{condition.ColumnName}' referenced in condition not found in input data",
                        ValidationExceptionErrorCode.General);
                }
            }
        }

        // Apply conditional logic to each record
        int matchCounts = 0;
        int elseCounts = 0;

        foreach (var record in records)
        {
            string resultValue = Options.ElseValue;
            bool matched = false;

            // Evaluate conditions in order (first match wins)
            foreach (var condition in _parsedConditions)
            {
                var columnValue = record.GetValueOrDefault(condition.ColumnName, string.Empty);

                if (condition.Evaluate(columnValue))
                {
                    resultValue = condition.ResultValue;
                    matched = true;
                    matchCounts++;
                    break;
                }
            }

            if (!matched)
            {
                elseCounts++;
            }

            // Set the output column value
            record[Options.OutputColumn] = resultValue;
        }

        _logger.LogInformation(
            "✓ Conditional column '{Column}' created: {MatchCount} matches, {ElseCount} else values, {TotalCount} total",
            Options.OutputColumn, matchCounts, elseCounts, records.Count);

        return await Task.FromResult(records);
    }

    protected override IEnumerable<string> GetRequiredColumns() => Array.Empty<string>();

    protected override string[] ValidateTaskSpecific(TaskContext context)
    {
        return Array.Empty<string>();
    }
}
