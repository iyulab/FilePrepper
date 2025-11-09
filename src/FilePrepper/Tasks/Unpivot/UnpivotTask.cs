namespace FilePrepper.Tasks.Unpivot;

/// <summary>
/// Task that performs unpivot (wide-to-long) transformation on data
/// Converts multiple column groups into rows with index and value columns
/// </summary>
public class UnpivotTask : BaseTask<UnpivotOption>
{
    public UnpivotTask(ILogger<UnpivotTask> logger) : base(logger)
    {
    }

    protected override Task<List<Dictionary<string, string>>> ProcessRecordsAsync(
        List<Dictionary<string, string>> records)
    {
        _logger.LogInformation("Starting unpivot transformation");
        _logger.LogInformation("Base columns: {BaseColumns}", string.Join(", ", Options.BaseColumns));
        _logger.LogInformation("Column groups to unpivot: {GroupCount}", Options.ColumnGroups.Count);

        var unpivotedRecords = new List<Dictionary<string, string>>();
        int skippedRows = 0;
        int totalRows = 0;

        foreach (var record in records)
        {
            var unpivotedRowsForRecord = UnpivotRecord(record);
            totalRows += unpivotedRowsForRecord.Count;

            if (Options.SkipEmptyRows)
            {
                var nonEmptyRows = unpivotedRowsForRecord.Where(r => !IsEmptyRow(r)).ToList();
                skippedRows += unpivotedRowsForRecord.Count - nonEmptyRows.Count;
                unpivotedRecords.AddRange(nonEmptyRows);
            }
            else
            {
                unpivotedRecords.AddRange(unpivotedRowsForRecord);
            }
        }

        _logger.LogInformation("Unpivot complete: {InputRows} wide rows → {OutputRows} long rows",
            records.Count, unpivotedRecords.Count);

        if (Options.SkipEmptyRows && skippedRows > 0)
        {
            _logger.LogInformation("Skipped {SkippedRows} empty rows", skippedRows);
        }

        return Task.FromResult(unpivotedRecords);
    }

    private List<Dictionary<string, string>> UnpivotRecord(Dictionary<string, string> record)
    {
        var unpivotedRows = new List<Dictionary<string, string>>();

        for (int i = 0; i < Options.ColumnGroups.Count; i++)
        {
            var group = Options.ColumnGroups[i];
            var unpivotedRow = new Dictionary<string, string>();

            // Add base columns (common to all output rows)
            foreach (var baseColumn in Options.BaseColumns)
            {
                unpivotedRow[baseColumn] = record.ContainsKey(baseColumn)
                    ? record[baseColumn]
                    : string.Empty;
            }

            // Add index column
            var indexValue = group.IndexValue ?? (i + 1).ToString();
            unpivotedRow[Options.IndexColumnName] = indexValue;

            // Add value columns from the current group
            for (int j = 0; j < group.Columns.Count; j++)
            {
                var sourceColumn = group.Columns[j];
                var targetColumn = Options.ValueColumnNames[j];

                unpivotedRow[targetColumn] = record.ContainsKey(sourceColumn)
                    ? record[sourceColumn]
                    : string.Empty;
            }

            unpivotedRows.Add(unpivotedRow);
        }

        return unpivotedRows;
    }

    private bool IsEmptyRow(Dictionary<string, string> row)
    {
        // Check if all value columns are empty or zero
        return Options.ValueColumnNames.All(valueColumn =>
        {
            var value = row.GetValueOrDefault(valueColumn, string.Empty);
            return string.IsNullOrWhiteSpace(value) || value == "0";
        });
    }

    protected override string[] ValidateTaskSpecific(TaskContext context)
    {
        var errors = new List<string>();

        // Read headers to validate column existence
        var headers = GetFileHeaders(context.InputPath!);

        // Validate base columns exist
        foreach (var baseColumn in Options.BaseColumns)
        {
            if (!headers.Contains(baseColumn))
            {
                errors.Add($"Base column '{baseColumn}' not found in input file");
            }
        }

        // Validate column group columns exist
        foreach (var (group, groupIndex) in Options.ColumnGroups.Select((g, i) => (g, i)))
        {
            foreach (var column in group.Columns)
            {
                if (!headers.Contains(column))
                {
                    if (!Options.IgnoreErrors)
                    {
                        errors.Add($"Column '{column}' in group {groupIndex + 1} not found in input file");
                    }
                    else
                    {
                        _logger.LogWarning("Column '{Column}' in group {GroupIndex} not found in input file (ignored)",
                            column, groupIndex + 1);
                    }
                }
            }
        }

        return [.. errors];
    }
}
