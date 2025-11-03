namespace FilePrepper.Tasks.CreateLagFeatures;

/// <summary>
/// Create lag features from time series data for machine learning
/// </summary>
public class CreateLagFeaturesTask : BaseTask<CreateLagFeaturesOption>
{
    public CreateLagFeaturesTask(ILogger<CreateLagFeaturesTask> logger) : base(logger)
    {
    }

    protected override Task<List<Dictionary<string, string>>> ProcessRecordsAsync(
        List<Dictionary<string, string>> records)
    {
        _logger.LogInformation("Creating lag features for {Count} records", records.Count);

        // Group records by GroupByColumn
        var grouped = records
            .GroupBy(r => r.GetValueOrDefault(Options.GroupByColumn, string.Empty))
            .ToList();

        _logger.LogInformation("Grouped into {GroupCount} groups by column '{Column}'",
            grouped.Count, Options.GroupByColumn);

        var result = new List<Dictionary<string, string>>();

        foreach (var group in grouped)
        {
            var groupKey = group.Key;

            // Sort by TimeColumn within each group
            var sortedRecords = group
                .OrderBy(r => r.GetValueOrDefault(Options.TimeColumn, string.Empty))
                .ToList();

            _logger.LogDebug("Processing group '{Key}' with {Count} records", groupKey, sortedRecords.Count);

            // Create lag features for each record in the group
            for (int i = 0; i < sortedRecords.Count; i++)
            {
                var currentRecord = sortedRecords[i];
                var newRecord = new Dictionary<string, string>();

                // Keep GroupByColumn
                newRecord[Options.GroupByColumn] = groupKey;

                // Keep KeepColumns
                foreach (var keepCol in Options.KeepColumns)
                {
                    if (currentRecord.TryGetValue(keepCol, out var value))
                    {
                        newRecord[keepCol] = value;
                    }
                }

                // Create lag features
                bool hasNullLag = false;
                foreach (var lagColumn in Options.LagColumns)
                {
                    foreach (var lagPeriod in Options.LagPeriods)
                    {
                        var lagIndex = i - lagPeriod;
                        var lagColumnName = $"{lagColumn}_lag{lagPeriod}";

                        if (lagIndex >= 0 && lagIndex < sortedRecords.Count)
                        {
                            // Get value from previous time step
                            var lagValue = sortedRecords[lagIndex].GetValueOrDefault(lagColumn, string.Empty);
                            newRecord[lagColumnName] = lagValue;

                            if (string.IsNullOrWhiteSpace(lagValue))
                            {
                                hasNullLag = true;
                            }
                        }
                        else
                        {
                            // No data available for this lag
                            newRecord[lagColumnName] = string.Empty;
                            hasNullLag = true;
                        }
                    }
                }

                // Keep TargetColumn
                if (!string.IsNullOrWhiteSpace(Options.TargetColumn))
                {
                    if (currentRecord.TryGetValue(Options.TargetColumn, out var targetValue))
                    {
                        newRecord[Options.TargetColumn] = targetValue;
                    }
                }

                // Add record if it doesn't have null lags (or if we're keeping null rows)
                if (!Options.DropNullRows || !hasNullLag)
                {
                    result.Add(newRecord);
                }
            }
        }

        _logger.LogInformation("Created {Count} records with lag features", result.Count);

        if (Options.DropNullRows)
        {
            var droppedCount = records.Count - result.Count;
            _logger.LogInformation("Dropped {Count} rows with null lag values", droppedCount);
        }

        return Task.FromResult(result);
    }

    protected override string[] ValidateTaskSpecific(TaskContext context)
    {
        var errors = new List<string>();

        // Validate that required columns exist in the data
        var headers = GetFileHeaders(context.InputPath);

        if (!headers.Contains(Options.GroupByColumn))
        {
            errors.Add($"GroupByColumn '{Options.GroupByColumn}' not found in input file");
        }

        if (!headers.Contains(Options.TimeColumn))
        {
            errors.Add($"TimeColumn '{Options.TimeColumn}' not found in input file");
        }

        foreach (var lagCol in Options.LagColumns)
        {
            if (!headers.Contains(lagCol))
            {
                errors.Add($"LagColumn '{lagCol}' not found in input file");
            }
        }

        if (!string.IsNullOrWhiteSpace(Options.TargetColumn) &&
            !headers.Contains(Options.TargetColumn))
        {
            errors.Add($"TargetColumn '{Options.TargetColumn}' not found in input file");
        }

        return [.. errors];
    }
}
