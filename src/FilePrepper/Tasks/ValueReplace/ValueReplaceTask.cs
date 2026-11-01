namespace FilePrepper.Tasks.ValueReplace;

public class ValueReplaceTask : BaseTask<ValueReplaceOption>
{
    public ValueReplaceTask(ILogger<ValueReplaceTask> logger) : base(logger)
    {
    }

    protected override IEnumerable<string> GetRequiredColumns() => Options.ReplaceMethods.Select(method => method.ColumnName);

    protected override Task<List<Dictionary<string, string>>> ProcessRecordsAsync(
        List<Dictionary<string, string>> records)
    {
        foreach (var method in Options.ReplaceMethods)
        {
            foreach (var record in records)
            {
                var colName = method.ColumnName;
                if (record.TryGetValue(colName, out string? value))
                {
                    if (method.Replacements.TryGetValue(value, out string? v))
                    {
                        record[colName] = v;
                    }
                }
            }
        }

        return Task.FromResult(records);
    }
}
