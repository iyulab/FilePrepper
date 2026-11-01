namespace FilePrepper.Tasks.RenameColumns;

public class RenameColumnsTask : BaseTask<RenameColumnsOption>
{
    public RenameColumnsTask(ILogger<RenameColumnsTask> logger) : base(logger)
    {
    }

    protected override Task<List<Dictionary<string, string>>> ProcessRecordsAsync(
        List<Dictionary<string, string>> records)
    {
        _logger.LogInformation("Renaming specified columns in records");

        // Renaming a column that is not there is skipped, but a mistyped name leaves the column as it was: say so.
        WarnOfNamesNotInTheInput(Options.RenameMap.Keys, "nothing to rename");

        var renameMap = Options.RenameMap;
        // 원본 헤더 순서를 보존하기 위해 복사본 생성
        var oldHeadersCopy = new List<string>(_originalHeaders);
        var newHeaderOrder = new List<string>();
        foreach (var col in oldHeadersCopy)
        {
            newHeaderOrder.Add(renameMap.TryGetValue(col, out string? value) ? value : col);
        }
        // 출력용 헤더 순서 업데이트
        _originalHeaders = newHeaderOrder;

        // 각 레코드에서 열 이름 변경 (순서 보존)
        foreach (var record in records)
        {
            var newRecord = new Dictionary<string, string>();
            foreach (var col in oldHeadersCopy)
            {
                string newColName = renameMap.TryGetValue(col, out string? v) ? v : col;
                if (record.TryGetValue(col, out string? value))
                {
                    newRecord[newColName] = value;
                }
                else
                {
                    newRecord[newColName] = string.Empty;
                }
            }
            record.Clear();
            foreach (var kv in newRecord)
            {
                record[kv.Key] = kv.Value;
            }
        }

        return Task.FromResult(records);
    }
}
