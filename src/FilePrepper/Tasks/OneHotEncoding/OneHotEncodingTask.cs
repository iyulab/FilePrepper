namespace FilePrepper.Tasks.OneHotEncoding;

/// <summary>
/// Perform One-Hot Encoding on specified categorical columns.
/// </summary>
public class OneHotEncodingTask : BaseTask<OneHotEncodingOption>
{
    public OneHotEncodingTask(ILogger<OneHotEncodingTask> logger) : base(logger)
    {
    }

    protected override bool ColumnsDependOnTheRows => true;

    protected override Task<List<Dictionary<string, string>>> ProcessRecordsAsync(
        List<Dictionary<string, string>> records)
    {
        if (records.Count == 0)
            return Task.FromResult(records);

        // 1) Collect all distinct categories for each target column
        var categoryMap = new Dictionary<string, List<string>>();
        foreach (var col in Options.TargetColumns)
        {
            categoryMap[col] = new List<string>();
        }

        foreach (var rec in records)
        {
            foreach (var col in Options.TargetColumns)
            {
                if (rec.TryGetValue(col, out string? value))
                {
                    if (!categoryMap[col].Contains(value))
                    {
                        categoryMap[col].Add(value);
                    }
                }
            }
        }

        // Optionally, sort categories for consistent output
        foreach (var col in categoryMap.Keys)
        {
            categoryMap[col].Sort(StringComparer.Ordinal); // the same column order on every machine
        }

        // 2) Create new columns and fill values
        // We'll do it in-place: add new columns for each category
        // then optionally remove original columns afterwards.
        foreach (var rec in records)
        {
            foreach (var col in Options.TargetColumns)
            {
                if (!rec.ContainsKey(col))
                    continue; // If missing, skip

                var originalValue = rec[col];
                var allCategories = categoryMap[col];

                // If DropFirst=true, skip the first category in the loop below
                int startIndex = Options.DropFirst ? 1 : 0;

                for (int i = startIndex; i < allCategories.Count; i++)
                {
                    var cat = allCategories[i];
                    // e.g. "Color=Red" => new column "Color_Red"
                    string newColName = $"{col}_{cat}";
                    // 1 if matches, else 0
                    rec[newColName] = (originalValue == cat) ? "1" : "0";
                }
            }
        }

        // 3) Remove original columns if desired
        if (!Options.KeepOriginalColumns)
        {
            foreach (var rec in records)
            {
                foreach (var col in Options.TargetColumns)
                {
                    rec.Remove(col);
                }
            }
        }

        return Task.FromResult(records);
    }

    protected override IEnumerable<string> GetRequiredColumns()
    {
        // In many use cases, we don't want to forcibly fail if these columns are missing,
        // but if you do, you could return Options.TargetColumns.
        return Options.TargetColumns;
    }

    protected override string[] ValidateTaskSpecific(TaskContext context)
    {
        var errors = new List<string>();
        var headers = GetFileHeaders(context.InputPath);

        // 타겟 컬럼 존재 여부 검증
        foreach (var column in Options.TargetColumns)
        {
            if (!headers.Contains(column))
            {
                errors.Add($"Target column '{column}' not found in file");
            }
        }

        return [.. errors];
    }
}