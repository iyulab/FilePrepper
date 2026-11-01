using Microsoft.Extensions.Logging;

namespace FilePrepper.Tasks.RemoveConstants;

/// <summary>
/// Removes the columns that carry no variety: every row holds the same value, or — with a
/// <see cref="RemoveConstantsOption.UniqueRatioThreshold"/> — the column has few distinct values for
/// the number of rows.
/// </summary>
/// <remarks>
/// A column's values are compared exactly, as text: <c>1</c> and <c>1.0</c>, or <c>a</c> and <c>A</c>,
/// are different values. An empty cell is a value like any other, so a column that is empty in every
/// row is constant, while a column holding one value and some empty cells has two values and stays.
/// <para>
/// The ratio is distinct values ÷ rows. It measures how many different values a column has, not how
/// much one value dominates: a three-level category over 10,000 rows has a ratio of 0.03 % and is
/// removed at a threshold of 0.01, and a column that is 99 % one value but has many rare values stays.
/// </para>
/// <para>
/// A table that would be left with no column is refused (every column of a one-row file is constant):
/// the output would otherwise be a placeholder header over blank lines, written as if it had worked.
/// </para>
/// </remarks>
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
            var uniqueValues = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in records)
            {
                if (record.TryGetValue(header, out var value))
                {
                    uniqueValues.Add(value);
                }
            }

            var uniqueCount = uniqueValues.Count;
            var uniqueRatio = (double)uniqueCount / records.Count;
            var isConstant = uniqueCount <= 1;
            var isLowVariety = Options.UniqueRatioThreshold > 0 && uniqueRatio <= Options.UniqueRatioThreshold;

            if (isConstant || isLowVariety)
            {
                var reason = isConstant ? "constant" : $"low-variety (unique ratio: {uniqueRatio:P2})";
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

        _logger.LogInformation("Found {Count} constant/low-variety column(s): {Columns}",
            columnsToRemove.Count, string.Join(", ", columnsToRemove));

        if (Options.ReportOnly)
        {
            _logger.LogInformation("Report-only mode: columns will not be removed");
            return Task.FromResult(records);
        }

        if (columnsToRemove.Count == headers.Count)
        {
            throw new InvalidOperationException(
                $"All {headers.Count} column(s) are constant or low-variety, so removing them would leave an empty table " +
                $"({records.Count} row(s) read). Use --report-only to list the columns, or lower the threshold.");
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
