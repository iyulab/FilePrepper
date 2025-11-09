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

        // Create DataPipeline from records
        var pipeline = DataPipeline.FromData(records);

        // Apply window operation based on type
        DataPipeline result = Options.Type switch
        {
            WindowType.Resample => ApplyResample(pipeline),
            WindowType.Rolling => ApplyRolling(pipeline),
            _ => throw new ArgumentException($"Unknown window type: {Options.Type}")
        };

        // Convert back to records
        var dataFrame = result.ToDataFrame();
        var processedRecords = dataFrame.Rows.ToList();

        _logger.LogInformation("✓ Processed {InputCount} rows → {OutputCount} rows",
            records.Count, processedRecords.Count);

        return await Task.FromResult(processedRecords);
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
