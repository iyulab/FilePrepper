using FilePrepper.Utils;

namespace FilePrepper.Tasks.SplitList;

/// <summary>
/// Splits list columns of one length into one column per position — see
/// <see cref="ListColumns.SplitToColumns"/>.
/// </summary>
public class SplitListTask : BaseTask<SplitListOption>
{
    public SplitListTask(ILogger<SplitListTask> logger) : base(logger)
    {
    }

    protected override Task<List<Dictionary<string, string>>> ProcessRecordsAsync(
        List<Dictionary<string, string>> records)
    {
        var (rows, headers) = ListColumns.SplitToColumns(records, _originalHeaders, Options.Columns);
        _originalHeaders = headers;
        _logger.LogInformation("Split {Count} list column(s) into {Width} columns", Options.Columns.Count, headers.Count);
        return Task.FromResult(rows);
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
