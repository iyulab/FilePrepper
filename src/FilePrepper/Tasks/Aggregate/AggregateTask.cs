namespace FilePrepper.Tasks.Aggregate;

public class AggregateTask : BaseTask<AggregateOption>
{
    public AggregateTask(ILogger<AggregateTask> logger) : base(logger)
    {
    }

    protected override async Task<List<Dictionary<string, string>>> ProcessRecordsAsync(
        List<Dictionary<string, string>> records)
    {
        // 그룹화
        var groups = await GroupRecordsAsync(records);

        // 집계 계산
        var groupResults = await CalculateAggregatesAsync(groups);

        // 결과 포맷팅
        return Options.AppendToSource
            ? await WriteAppendedResultsAsync(records, groupResults)
            : await WriteAggregateResultsAsync(groupResults);
    }

    private async Task<Dictionary<string, List<Dictionary<string, string>>>> GroupRecordsAsync(
        List<Dictionary<string, string>> records)
    {
        return await Task.Run(() =>
            records.GroupBy(record =>
                GroupKey(record))
                .ToDictionary(g => g.Key, g => g.ToList()));
    }

    /// <summary>
    /// What joins the values of a row's group columns into one key. It was <c>|</c>, so a value that held one split into two group
    /// columns' worth and ("a|b","c") became the group of ("a","b|c"); a unit separator is not in a data cell.
    /// </summary>
    private const char KeySeparator = '\u001F';

    private string GroupKey(Dictionary<string, string> record) =>
        string.Join(KeySeparator, Options.GroupByColumns.Select(col => record[col]));

    private async Task<Dictionary<string, Dictionary<string, string>>> CalculateAggregatesAsync(
        Dictionary<string, List<Dictionary<string, string>>> groups)
    {
        return await Task.Run(() =>
        {
            var results = new Dictionary<string, Dictionary<string, string>>();
            var leftOut = new Dictionary<string, (int Count, string? Example)>(StringComparer.Ordinal);

            foreach (var group in groups)
            {
                var result = new Dictionary<string, string>();
                var keyValues = group.Key.Split(KeySeparator);

                // 그룹 키 설정
                for (int i = 0; i < Options.GroupByColumns.Length; i++)
                {
                    result[Options.GroupByColumns[i]] = keyValues[i];
                }

                // 집계 계산
                foreach (var aggCol in Options.AggregateColumns)
                {
                    var values = new List<double>();
                    foreach (var record in group.Value)
                    {
                        var cell = record[aggCol.ColumnName];
                        if (CsvUtils.TryParseNumeric(cell, out var value))
                        {
                            values.Add(value);
                        }
                        else if (!string.IsNullOrWhiteSpace(cell))
                        {
                            leftOut.TryGetValue(aggCol.ColumnName, out var seen);
                            leftOut[aggCol.ColumnName] = (seen.Count + 1, seen.Example ?? cell);
                        }
                    }

                    if (values.Count != 0)
                    {
                        var aggregateValue = CalculateAggregate(values, aggCol.Function);
                        var outputColumnName = GetAggregateColumnName(aggCol);
                        result[outputColumnName] = NumberText.Format(aggregateValue);
                    }
                }

                results[group.Key] = result;
            }

            foreach (var (column, (count, example)) in leftOut)
            {
                _logger.LogWarning("{Count} cell(s) of column '{Column}' are not numbers and were left out of the aggregate (first: '{Example}')",
                    count, column, example);
            }

            return results;
        });
    }

    private async Task<List<Dictionary<string, string>>> WriteAppendedResultsAsync(
        List<Dictionary<string, string>> records,
        Dictionary<string, Dictionary<string, string>> groupResults)
    {
        return await Task.Run(() =>
        {
            foreach (var record in records)
            {
                var groupKey = GroupKey(record);
                if (groupResults.TryGetValue(groupKey, out var groupResult))
                {
                    foreach (var (key, value) in groupResult.Where(kvp =>
                        !Options.GroupByColumns.Contains(kvp.Key)))
                    {
                        record[key] = value;
                    }
                }
            }
            return records;
        });
    }

    private async Task<List<Dictionary<string, string>>> WriteAggregateResultsAsync(
        Dictionary<string, Dictionary<string, string>> groupResults)
    {
        return await Task.Run(() => groupResults.Values.ToList());
    }

    private string GetAggregateColumnName(AggregateColumn aggCol)
    {
        if (!Options.AppendToSource)
        {
            return aggCol.OutputColumnName;
        }

        return Options.OutputColumnTemplate!
            .Replace("{column}", aggCol.ColumnName)
            .Replace("{function}", aggCol.Function.ToString())
            .Replace("{groupBy}", string.Join("_", Options.GroupByColumns));
    }

    private double CalculateAggregate(List<double> values, AggregateFunction function)
    {
        return function switch
        {
            AggregateFunction.Sum => values.Sum(),
            AggregateFunction.Average => MathUtils.CalculateMean(values),
            AggregateFunction.Count => values.Count,
            AggregateFunction.Min => values.Min(),
            AggregateFunction.Max => values.Max(),
            _ => throw new ArgumentException($"Unsupported function: {function}")
        };
    }

    protected override IEnumerable<string> GetRequiredColumns()
    {
        return Options.GroupByColumns.Concat(
            Options.AggregateColumns.Select(x => x.ColumnName));
    }
}
