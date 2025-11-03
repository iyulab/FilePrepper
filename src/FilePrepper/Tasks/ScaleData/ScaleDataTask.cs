namespace FilePrepper.Tasks.ScaleData;

public class ScaleDataTask : BaseTask<ScaleDataOption>
{
    public ScaleDataTask(ILogger<ScaleDataTask> logger) : base(logger)
    {
    }

    protected override async Task<List<Dictionary<string, string>>> ProcessRecordsAsync(
        List<Dictionary<string, string>> records)
    {
        foreach (var colOption in Options.ScaleColumns)
        {
            await ScaleColumn(records, colOption);
        }
        return records;
    }

    private Task ScaleColumn(
        List<Dictionary<string, string>> records,
        ScaleColumnOption colOption)
    {
        // 유효한 수치형 값 추출
        var numericValues = records
            .Select(r => r.GetValueOrDefault(colOption.ColumnName))
            .Where(v => double.TryParse(v, out _))
            .Select(v => double.Parse(v!))
            .ToList();

        if (numericValues.Count == 0)
        {
            return Task.CompletedTask;
        }

        if (colOption.Method == ScaleMethod.MinMax)
        {
            double min = numericValues.Min();
            double max = numericValues.Max();

            foreach (var record in records)
            {
                if (double.TryParse(record.GetValueOrDefault(colOption.ColumnName), out double value))
                {
                    double scaled = (max - min == 0) ? 0 : (value - min) / (max - min);
                    record[colOption.ColumnName] = scaled.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }
        }
        else if (colOption.Method == ScaleMethod.Standardization)
        {
            double mean = numericValues.Average();
            double sumSquares = numericValues.Sum(v => (v - mean) * (v - mean));
            double std = Math.Sqrt(sumSquares / numericValues.Count);

            foreach (var record in records)
            {
                if (double.TryParse(record.GetValueOrDefault(colOption.ColumnName), out double value))
                {
                    double scaled = (std == 0) ? 0 : (value - mean) / std;
                    record[colOption.ColumnName] = scaled.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }
        }

        return Task.CompletedTask;
    }
}