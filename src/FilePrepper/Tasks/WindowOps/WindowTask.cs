using FilePrepper.Pipeline;

namespace FilePrepper.Tasks.WindowOps;

/// <summary>
/// Window operations task for time-series resampling and rolling aggregations
/// </summary>
public class WindowTask : BaseTask<WindowOption>
{
    public WindowTask(ILogger<WindowTask> logger) : base(logger) { }

    protected override async Task<List<Dictionary<string, string>>> ProcessRecordsAsync(
        List<Dictionary<string, string>> records)
    {
        _logger.LogInformation("Starting Window operation: {Type}", Options.Type);
        _logger.LogInformation("  Aggregation method: {Method}", Options.Method);
        _logger.LogInformation("  Target columns: {Columns}", string.Join(", ", Options.TargetColumns ?? Array.Empty<string>()));

        if (Options.Type == WindowType.Resample)
        {
            WarnOfRowsWithoutATime(records);
        }

        // Create DataPipeline from records
        var pipeline = DataPipeline.FromData(records);

        // Apply window operation based on type
        DataPipeline result = Options.Type switch
        {
            WindowType.Resample => ApplyResample(pipeline),
            WindowType.Rolling => ApplyRolling(pipeline),
            _ => throw new ArgumentException($"Unknown window type: {Options.Type}")
        };

        // Convert back to records. Every record carries every column of the result: the writer takes its header from the
        // records, and a resampled window that had no value for a column left the key out, so a column empty in the
        // first window was missing from the file altogether, the later windows' values with it.
        var dataFrame = result.ToDataFrame();
        var columns = dataFrame.ColumnNames.Distinct(StringComparer.Ordinal).ToList();
        var processedRecords = dataFrame.Rows
            .Select(row => columns.ToDictionary(
                column => column,
                column => row.TryGetValue(column, out var value) ? value : string.Empty))
            .ToList();

        if (records.Count > 0 && processedRecords.Count == 0)
        {
            throw new InvalidOperationException(
                $"None of the {records.Count} row(s) has a time in column '{Options.TimeColumn}' that can be read, so there is no window to resample.");
        }

        var outputColumn = (string column) => Options.Type == WindowType.Rolling ? column + Options.OutputSuffix : column;
        foreach (var column in (Options.TargetColumns ?? []).Distinct(StringComparer.Ordinal))
        {
            var name = outputColumn(column);
            if (processedRecords.Count > 0 && processedRecords.All(record => string.IsNullOrEmpty(record.GetValueOrDefault(name))))
            {
                _logger.LogWarning(
                    "Column '{Column}' has no numeric value to aggregate, so '{Output}' is empty in every row (is the name right, and is the column numeric?)",
                    column, name);
            }
        }

        _logger.LogInformation("✓ Processed {InputCount} rows → {OutputCount} rows",
            records.Count, processedRecords.Count);

        return await Task.FromResult(processedRecords);
    }

    /// <summary>
    /// A row whose time cell is empty or unreadable belongs to no window and is left out of the result: say how many, and
    /// which value was first, rather than let the aggregates quietly describe fewer rows than the file has.
    /// </summary>
    private void WarnOfRowsWithoutATime(List<Dictionary<string, string>> records)
    {
        if (string.IsNullOrEmpty(Options.TimeColumn))
            return;

        var left = records
            .Select((record, index) => (record, index))
            .Where(item => !ResampleWindow.HasReadableTime(item.record, Options.TimeColumn!))
            .ToList();
        if (left.Count == 0)
            return;

        var first = left[0];
        _logger.LogWarning(
            "{Count} of {Total} row(s) left out: no readable time in column '{Column}' (first: data row {Row}, value '{Value}')",
            left.Count, records.Count, Options.TimeColumn, first.index + 1, first.record.GetValueOrDefault(Options.TimeColumn!, string.Empty));
    }

    private DataPipeline ApplyResample(DataPipeline pipeline)
    {
        if (string.IsNullOrEmpty(Options.TimeColumn))
        {
            throw new ArgumentException("TimeColumn is required for Resample operation");
        }

        if (string.IsNullOrEmpty(Options.Window))
        {
            throw new ArgumentException("Window is required for Resample operation");
        }

        _logger.LogInformation("  Time column: {TimeColumn}", Options.TimeColumn);
        _logger.LogInformation("  Window: {Window}", Options.Window);

        return pipeline.Resample(
            Options.TimeColumn!,
            Options.Window!,
            Options.Method,
            Options.TargetColumns ?? Array.Empty<string>()
        );
    }

    private DataPipeline ApplyRolling(DataPipeline pipeline)
    {
        _logger.LogInformation("  Window size: {Size} rows", Options.WindowSize);
        _logger.LogInformation("  Output suffix: {Suffix}", Options.OutputSuffix);

        return pipeline.Rolling(
            Options.WindowSize,
            Options.Method,
            Options.TargetColumns ?? Array.Empty<string>(),
            Options.OutputSuffix
        );
    }
}
