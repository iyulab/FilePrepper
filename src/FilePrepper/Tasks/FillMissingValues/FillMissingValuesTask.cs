using FilePrepper.Utils;
﻿namespace FilePrepper.Tasks.FillMissingValues;

public class FillMissingValuesTask : BaseTask<FillMissingValuesOption>
{
    public FillMissingValuesTask(ILogger<FillMissingValuesTask> logger) : base(logger)
    {
    }

    protected override async Task<List<Dictionary<string, string>>> ProcessRecordsAsync(
        List<Dictionary<string, string>> records)
    {
        foreach (var fillMethod in Options.FillMethods)
        {
            await FillMissingValuesForColumn(records, fillMethod);
        }

        return records;
    }

    private async Task FillMissingValuesForColumn(
        List<Dictionary<string, string>> records,
        ColumnFillMethod fillMethod)
    {
        // 빈 문자열 또는 숫자 파싱 실패도 결측치로 처리
        bool hasMissing = records.Any(r =>
        {
            var value = r.GetValueOrDefault(fillMethod.ColumnName);
            // null/whitespace인 경우
            if (string.IsNullOrWhiteSpace(value))
                return true;

            // 정수/실수로 파싱 안 되면 => 결측치로 간주
            return !NumberText.TryParse(value, out _);
        });

        if (!hasMissing)
        {
            return;
        }

        // 결측치 채우기
        switch (fillMethod.Method)
        {
            case FillMethod.FixedValue:
                await FillWithFixedValue(records, fillMethod);
                break;
            case FillMethod.Mean:
                await FillWithMean(records, fillMethod);
                break;
            case FillMethod.Median:
                await FillWithMedian(records, fillMethod);
                break;
            case FillMethod.Mode:
                await FillWithMode(records, fillMethod);
                break;
            case FillMethod.ForwardFill:
                await FillForward(records, fillMethod);
                break;
            case FillMethod.BackwardFill:
                await FillBackward(records, fillMethod);
                break;
            case FillMethod.LinearInterpolation:
                await FillWithLinearInterpolation(records, fillMethod);
                break;
            default:
                throw new ArgumentException($"Unsupported fill method: {fillMethod.Method}");
        }
    }

    private Task FillWithFixedValue(
        List<Dictionary<string, string>> records,
        ColumnFillMethod fillMethod)
    {
        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.GetValueOrDefault(fillMethod.ColumnName)))
            {
                record[fillMethod.ColumnName] = fillMethod.FixedValue ?? Options.DefaultValue ?? string.Empty;
            }
        }
        return Task.CompletedTask;
    }

    private async Task FillWithMean(
        List<Dictionary<string, string>> records,
        ColumnFillMethod fillMethod)
    {
        var validValues = records
            .Select(r => r.GetValueOrDefault(fillMethod.ColumnName))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => NumberText.TryParse(v, out var d) ? (double?)d : null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();

        if (validValues.Count == 0)
        {
            await FillWithDefaultValue(records, fillMethod);
            return;
        }

        var mean = MathUtils.CalculateMean(validValues);
        var meanStr = mean.ToString(System.Globalization.CultureInfo.InvariantCulture);

        foreach (var record in records)
        {
            var currentValue = record.GetValueOrDefault(fillMethod.ColumnName);
            if (string.IsNullOrWhiteSpace(currentValue) ||
                !NumberText.TryParse(currentValue, out _))
            {
                record[fillMethod.ColumnName] = meanStr;
            }
        }
    }

    private async Task FillWithMedian(
        List<Dictionary<string, string>> records,
        ColumnFillMethod fillMethod)
    {
        var validValues = records
            .Select(r => r.GetValueOrDefault(fillMethod.ColumnName))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => NumberText.TryParse(v, out var d) ? d : double.NaN)
            .Where(v => !double.IsNaN(v))
            .ToList();

        if (validValues.Count == 0)
        {
            await FillWithDefaultValue(records, fillMethod);
            return;
        }

        var median = MathUtils.CalculateMedian(validValues);
        var medianStr = median.ToString(System.Globalization.CultureInfo.InvariantCulture);

        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.GetValueOrDefault(fillMethod.ColumnName)) ||
                !NumberText.TryParse(record[fillMethod.ColumnName], out _))
            {
                record[fillMethod.ColumnName] = medianStr;
            }
        }
    }

    private async Task FillWithMode(
        List<Dictionary<string, string>> records,
        ColumnFillMethod fillMethod)
    {
        var mode = records
            .Select(r => r.GetValueOrDefault(fillMethod.ColumnName))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .GroupBy(v => v)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key;

        if (mode == null)
        {
            await FillWithDefaultValue(records, fillMethod);
            return;
        }

        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.GetValueOrDefault(fillMethod.ColumnName)))
            {
                record[fillMethod.ColumnName] = mode;
            }
        }
    }

    private Task FillForward(
        List<Dictionary<string, string>> records,
        ColumnFillMethod fillMethod)
    {
        string? lastValidValue = null;

        foreach (var record in records)
        {
            var currentValue = record.GetValueOrDefault(fillMethod.ColumnName);
            if (!string.IsNullOrWhiteSpace(currentValue))
            {
                lastValidValue = currentValue;
            }
            else if (lastValidValue != null)
            {
                record[fillMethod.ColumnName] = lastValidValue;
            }
        }

        return Task.CompletedTask;
    }

    private Task FillBackward(
        List<Dictionary<string, string>> records,
        ColumnFillMethod fillMethod)
    {
        string? nextValidValue = null;

        for (int i = records.Count - 1; i >= 0; i--)
        {
            var currentValue = records[i].GetValueOrDefault(fillMethod.ColumnName);
            if (!string.IsNullOrWhiteSpace(currentValue))
            {
                nextValidValue = currentValue;
            }
            else if (nextValidValue != null)
            {
                records[i][fillMethod.ColumnName] = nextValidValue;
            }
        }

        return Task.CompletedTask;
    }

    private Task FillWithLinearInterpolation(
        List<Dictionary<string, string>> records,
        ColumnFillMethod fillMethod)
    {
        // 숫자값만 추출하여 인덱스와 함께 저장
        var numericValues = new List<(int Index, double Value)>();
        for (int i = 0; i < records.Count; i++)
        {
            var value = records[i].GetValueOrDefault(fillMethod.ColumnName);
            if (!string.IsNullOrWhiteSpace(value) && NumberText.TryParse(value, out var numValue))
            {
                numericValues.Add((i, numValue));
            }
        }

        // 각 missing value에 대해 보간 수행
        for (int i = 0; i < records.Count; i++)
        {
            var currentValue = records[i].GetValueOrDefault(fillMethod.ColumnName);
            if (!string.IsNullOrWhiteSpace(currentValue) && NumberText.TryParse(currentValue, out _))
            {
                continue;
            }

            // 현재 위치 기준으로 가장 가까운 앞뒤 숫자값 찾기
            var prevValue = numericValues.LastOrDefault(x => x.Index < i);
            var nextValue = numericValues.FirstOrDefault(x => x.Index > i);

            if (prevValue != default && nextValue != default)
            {
                // 보간 수행
                var ratio = (double)(i - prevValue.Index) / (nextValue.Index - prevValue.Index);
                var interpolated = prevValue.Value + (nextValue.Value - prevValue.Value) * ratio;
                records[i][fillMethod.ColumnName] = interpolated.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                // 보간할 수 없는 경우 기본값 사용
                records[i][fillMethod.ColumnName] = Options.DefaultValue ?? string.Empty;
            }
        }

        return Task.CompletedTask;
    }

    private int FindPreviousValidIndex(
        List<Dictionary<string, string>> records,
        string columnName,
        int currentIndex)
    {
        for (int i = currentIndex - 1; i >= 0; i--)
        {
            var value = records[i].GetValueOrDefault(columnName);
            if (!string.IsNullOrWhiteSpace(value) && NumberText.TryParse(value, out _))
            {
                return i;
            }
        }
        return -1;
    }

    private int FindNextValidIndex(
        List<Dictionary<string, string>> records,
        string columnName,
        int currentIndex)
    {
        for (int i = currentIndex + 1; i < records.Count; i++)
        {
            var value = records[i].GetValueOrDefault(columnName);
            if (!string.IsNullOrWhiteSpace(value) && NumberText.TryParse(value, out _))
            {
                return i;
            }
        }
        return -1;
    }

    private Task FillWithDefaultValue(
        List<Dictionary<string, string>> records,
        ColumnFillMethod fillMethod)
    {
        var defaultValue = Options.DefaultValue ?? string.Empty;
        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.GetValueOrDefault(fillMethod.ColumnName)))
            {
                record[fillMethod.ColumnName] = defaultValue;
            }
        }
        return Task.CompletedTask;
    }

    protected override IEnumerable<string> GetRequiredColumns()
    {
        return Options.FillMethods.Select(m => m.ColumnName).Union(Options.TargetColumns);
    }

    protected override string[] ValidateTaskSpecific(TaskContext context)
    {
        var errors = new List<string>();
        var headers = GetFileHeaders(context.InputPath);

        // 모든 타겟 컬럼이 존재하는지 검증
        foreach (var method in Options.FillMethods)
        {
            if (!headers.Contains(method.ColumnName))
            {
                errors.Add($"Column '{method.ColumnName}' specified in FillMethods not found in file");
            }
        }

        return [.. errors];
    }
}