using FilePrepper.Utils;

namespace FilePrepper.Tasks.Explode;

/// <summary>
/// Turns list columns (a list per cell, as JSON text) into one row per element — see
/// <see cref="ListColumns.Explode"/>.
/// </summary>
public class ExplodeTask : BaseTask<ExplodeOption>
{
    public ExplodeTask(ILogger<ExplodeTask> logger) : base(logger)
    {
    }

    protected override Task<List<Dictionary<string, string>>> ProcessRecordsAsync(
        List<Dictionary<string, string>> records)
    {
        var exploded = ListColumns.Explode(records, _originalHeaders, Options.Columns);
        _logger.LogInformation("Explode complete: {InputRows} rows → {OutputRows} rows", records.Count, exploded.Count);
        return Task.FromResult(exploded);
    }

    protected override string[] ValidateTaskSpecific(TaskContext context)
    {
        var headers = GetFileHeaders(context.InputPath!);
        return Options.Columns
            .Where(c => !headers.Contains(c))
            .Select(c => $"Column '{c}' not found in input file")
            .ToArray();
    }
}
