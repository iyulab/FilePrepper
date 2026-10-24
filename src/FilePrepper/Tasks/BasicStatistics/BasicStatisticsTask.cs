using FilePrepper.Utils;
﻿namespace FilePrepper.Tasks.BasicStatistics;

public class BasicStatisticsTask : BaseTask<BasicStatisticsOption>
{
    public BasicStatisticsTask(ILogger<BasicStatisticsTask> logger) : base(logger)
    {
    }

    protected override async Task<List<Dictionary<string, string>>> ProcessRecordsAsync(
        List<Dictionary<string, string>> records)
    {
        // 수치 데이터 수집
        var columnData = await CollectNumericDataAsync(records);

        // 통계량 계산
        _logger.LogInformation("Calculating statistics");
        var columnStats = await CalculateColumnStatisticsAsync(columnData);

        // 통계량 추가
        await AddStatisticsToRecordsAsync(records, columnStats);

        return records;
    }

    private async Task<Dictionary<string, List<double>>> CollectNumericDataAsync(
    List<Dictionary<string, string>> records)
    {
        return await Task.Run(() =>
        {
            var columnData = new Dictionary<string, List<double>>();

            foreach (var column in Options.TargetColumns)
            {
                columnData[column] = new List<double>();

                foreach (var record in records)
                {
                    try
                    {
                        if (CsvUtils.TryParseNumeric(record[column], out var value))
                        {
                            columnData[column].Add(value);
                        }
                        else if (Options.IgnoreErrors &&
                                Options.DefaultValue != null)
                        {
                            if (NumberText.TryParse(Options.DefaultValue, out var defaultValue))
                            {
                                columnData[column].Add(defaultValue);
                            }
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Skipping invalid numeric value '{Value}' in column {Column}",
                                record[column],
                                column);
                        }
                    }
                    catch (Exception ex) when (Options.IgnoreErrors)
                    {
                        _logger.LogWarning(ex, "Error processing value for column {Column}", column);
                    }
                }
            }

            return columnData;
        });
    }

    private async Task<Dictionary<string, Dictionary<StatisticType, double>>> CalculateColumnStatisticsAsync(
        Dictionary<string, List<double>> columnData)
    {
        return await Task.Run(() =>
        {
            var columnStats = new Dictionary<string, Dictionary<StatisticType, double>>();

            foreach (var (column, values) in columnData)
            {
                if (values.Count == 0)
                {
                    _logger.LogWarning("No valid numeric data found in column {Column}", column);
                    continue;
                }

                var mean = MathUtils.CalculateMean(values);
                var median = MathUtils.CalculateMedian(values);
                var standardDeviation = MathUtils.CalculateStandardDeviation(values);
                var mad = MathUtils.CalculateMAD(values);
                var sortedValues = values.OrderBy(x => x).ToList();

                columnStats[column] = new Dictionary<StatisticType, double>
                {
                    [StatisticType.Mean] = mean,
                    [StatisticType.StandardDeviation] = standardDeviation,
                    [StatisticType.Min] = sortedValues.First(),
                    [StatisticType.Max] = sortedValues.Last(),
                    [StatisticType.Median] = median,
                    [StatisticType.Q1] = MathUtils.CalculateMedian(sortedValues.Take(sortedValues.Count / 2).ToList()),
                    [StatisticType.Q3] = MathUtils.CalculateMedian(sortedValues.Skip((sortedValues.Count + 1) / 2).ToList()),
                    [StatisticType.MAD] = mad
                };
            }

            return columnStats;
        });
    }

    private async Task AddStatisticsToRecordsAsync(
        List<Dictionary<string, string>> records,
        Dictionary<string, Dictionary<StatisticType, double>> columnStats)
    {
        var columnData = new Dictionary<string, List<double>>();
        foreach (var column in Options.TargetColumns)
        {
            columnData[column] = records
                .Where(r => CsvUtils.TryParseNumeric(r[column], out _))
                .Select(r => double.Parse(r[column], CultureInfo.InvariantCulture))
                .ToList();
        }

        await Task.Run(() =>
        {
            foreach (var record in records)
            {
                foreach (var column in Options.TargetColumns.Where(c => columnStats.ContainsKey(c)))
                {
                    var stats = columnStats[column];
                    if (!CsvUtils.TryParseNumeric(record[column], out var value))
                    {
                        foreach (var statType in Options.Statistics)
                        {
                            record[$"{column}{Options.Suffix}_{statType}"] = string.Empty;
                        }
                        continue;
                    }

                    foreach (var statType in Options.Statistics)
                    {
                        var columnName = $"{column}{Options.Suffix}_{statType}";
                        try
                        {
                            record[columnName] = CalculateStatistic(
                                value,
                                statType,
                                stats,
                                columnData[column]);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(
                                ex,
                                "Error calculating {StatType} for value {Value}",
                                statType,
                                value);
                            record[columnName] = string.Empty;
                        }
                    }
                }
            }
        });
    }

    private string CalculateStatistic(
        double value,
        StatisticType statType,
        Dictionary<StatisticType, double> stats,
        List<double> columnValues)
    {
        double result = statType switch
        {
            StatisticType.ZScore => MathUtils.CalculateZScore(
                value,
                stats[StatisticType.Mean],
                stats[StatisticType.StandardDeviation]),

            StatisticType.RobustZScore => MathUtils.CalculateRobustZScore(
                MathUtils.GetWinsorizedValue(
                    value,
                    stats[StatisticType.Median],
                    stats[StatisticType.MAD]),
                stats[StatisticType.Median],
                stats[StatisticType.MAD]),

            StatisticType.PercentRank => MathUtils.CalculatePercentRank(value, columnValues).Item1,

            _ => stats.TryGetValue(statType, out var val) ? val : 0
        };

        return result.ToString("F4", CultureInfo.InvariantCulture);
    }
}
