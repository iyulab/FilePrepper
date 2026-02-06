using Microsoft.Extensions.Logging;

namespace FilePrepper.Tasks.RemoveConstants;

public class RemoveConstantsTask : BaseTask<RemoveConstantsOption>
{
    public RemoveConstantsTask(ILogger<RemoveConstantsTask> logger) : base(logger)
    {
    }

    protected override Task<List<Dictionary<string, string>>> ProcessRecordsAsync(
        List<Dictionary<string, string>> records)
    {
        if (records.Count == 0)
        {
            _logger.LogWarning("No records to process");
            return Task.FromResult(records);
        }

        var headers = _originalHeaders.ToList();
        var columnsToRemove = new List<string>();

        foreach (var header in headers)
        {
            var uniqueValues = new HashSet<string>();
            foreach (var record in records)
            {
                if (record.TryGetValue(header, out var value))
                {
                    uniqueValues.Add(value);
                }
            }

            var uniqueCount = uniqueValues.Count;
            var isConstant = uniqueCount <= 1;
            var isNearConstant = Options.UniqueRatioThreshold > 0 &&
                                 records.Count > 0 &&
                                 (double)uniqueCount / records.Count <= Options.UniqueRatioThreshold;

            if (isConstant || isNearConstant)
            {
                var reason = isConstant ? "constant" : $"near-constant (unique ratio: {(double)uniqueCount / records.Count:P2})";
                _logger.LogInformation("Column '{Column}' is {Reason} ({UniqueCount} unique value(s))",
                    header, reason, uniqueCount);
                columnsToRemove.Add(header);
            }
        }

        if (columnsToRemove.Count == 0)
        {
            _logger.LogInformation("No constant columns found");
            return Task.FromResult(records);
        }

        _logger.LogInformation("Found {Count} constant/near-constant column(s): {Columns}",
            columnsToRemove.Count, string.Join(", ", columnsToRemove));

        if (Options.ReportOnly)
        {
            _logger.LogInformation("Report-only mode: columns will not be removed");
            return Task.FromResult(records);
        }

        // Remove columns from headers and records
        foreach (var col in columnsToRemove)
        {
            _originalHeaders.Remove(col);
        }

        foreach (var record in records)
        {
            foreach (var col in columnsToRemove)
            {
                record.Remove(col);
            }
        }

        _logger.LogInformation("Removed {Count} column(s)", columnsToRemove.Count);
        return Task.FromResult(records);
    }
}
