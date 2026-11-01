namespace FilePrepper.Tasks.CreateLagFeatures;

/// <summary>
/// Create lag features from time series data for machine learning
/// </summary>
/// <remarks>
/// <para>
/// Within each group the rows are put in time order and a lag of <c>n</c> is the value <c>n</c> <em>rows</em> earlier in the
/// group - not the value <c>n</c> periods earlier, so a gap in the time column is not a gap in the lags. The time column is
/// read as numbers when every value is a number, as dates when every value is a date, and as text (ordinal) otherwise;
/// rows with the same time stay in file order. A blank time sorts first.
/// </para>
/// <para>The output has the group column, the <c>KeepColumns</c>, the lag columns and the target column; the time column is in it only if it is a keep column.</para>
/// </remarks>
public class CreateLagFeaturesTask : BaseTask<CreateLagFeaturesOption>
{
    public CreateLagFeaturesTask(ILogger<CreateLagFeaturesTask> logger) : base(logger)
    {
    }

    /// <summary>
    /// The group column, the columns kept, one column per lag and the target — the columns of every row it makes, and so of the
    /// output when no row comes out (an input of one row has no row with a lag of its own to give).
    /// </summary>
    protected override IReadOnlyList<string>? DeclaredOutputHeaders()
    {
        var kept = _originalHeaders.ToHashSet(StringComparer.Ordinal);
        List<string> columns = [Options.GroupByColumn];
        columns.AddRange(Options.KeepColumns.Where(kept.Contains));
        columns.AddRange(Options.LagColumns.SelectMany(lagColumn => Options.LagPeriods.Select(period => $"{lagColumn}_lag{period}")));
        if (!string.IsNullOrWhiteSpace(Options.TargetColumn) && kept.Contains(Options.TargetColumn))
            columns.Add(Options.TargetColumn);
        return [.. columns.Distinct(StringComparer.Ordinal)];
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
        var order = ChooseTimeOrder(records);
        var repeatedTimes = 0;

        foreach (var group in grouped)
        {
            var groupKey = group.Key;

            // Sort by TimeColumn within each group
            var sortedRecords = SortByTime(group, order).ToList();
            repeatedTimes += sortedRecords.Zip(sortedRecords.Skip(1), (a, b) => TimeText(a) == TimeText(b)).Count(same => same);

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

        var blankTimes = records.Count(record => string.IsNullOrWhiteSpace(TimeText(record)));
        if (blankTimes > 0)
        {
            _logger.LogWarning("{Count} of {Total} row(s) have no value in time column '{Column}' and are placed first in their group",
                blankTimes, records.Count, Options.TimeColumn);
        }

        if (repeatedTimes > 0)
        {
            _logger.LogWarning("{Count} row(s) repeat the time of the row before them in their group; they stay in file order, so which one is the lag of the next is the file's order",
                repeatedTimes);
        }

        _logger.LogInformation("Created {Count} records with lag features", result.Count);

        if (Options.DropNullRows)
        {
            var droppedCount = records.Count - result.Count;
            _logger.LogInformation("Dropped {Count} rows with null lag values", droppedCount);
        }

        return Task.FromResult(result);
    }

    private enum TimeOrder { Number, Date, Text }

    private string TimeText(Dictionary<string, string> record) => record.GetValueOrDefault(Options.TimeColumn, string.Empty);

    /// <summary>
    /// How the time column is ordered, decided over the whole column so every group is ordered the same way: numbers when each
    /// value is a number, dates when each is a date. A text order puts "10" before "2", so it is the last resort and is said.
    /// </summary>
    private TimeOrder ChooseTimeOrder(List<Dictionary<string, string>> records)
    {
        var times = records.Select(TimeText).Where(time => !string.IsNullOrWhiteSpace(time)).ToList();
        if (times.Count > 0 && times.All(time => Utils.NumberText.TryParse(time, out _)))
            return TimeOrder.Number;

        if (times.Count > 0 && times.All(time => Utils.DateText.TryParse(time, out _)))
            return TimeOrder.Date;

        if (times.Count > 0)
        {
            _logger.LogWarning(
                "Time column '{Column}' is neither all numbers nor all dates, so its values are ordered as text (\"10\" before \"2\")",
                Options.TimeColumn);
        }

        return TimeOrder.Text;
    }

    private IEnumerable<Dictionary<string, string>> SortByTime(IEnumerable<Dictionary<string, string>> rows, TimeOrder order) => order switch
    {
        TimeOrder.Number => rows.OrderBy(row => Utils.NumberText.TryParse(TimeText(row), out var number) ? number : double.NegativeInfinity),
        TimeOrder.Date => rows.OrderBy(row => Utils.DateText.TryParse(TimeText(row), out var date) ? date : DateTime.MinValue),
        _ => rows.OrderBy(TimeText, StringComparer.Ordinal)
    };

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
