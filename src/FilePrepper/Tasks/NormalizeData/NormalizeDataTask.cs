using FilePrepper.Utils;
namespace FilePrepper.Tasks.NormalizeData;

/// <summary>
/// A task that normalizes numeric columns using either Min-Max or Z-score
/// </summary>
public class NormalizeDataTask : BaseTask<NormalizeDataOption>
{
    public NormalizeDataTask(ILogger<NormalizeDataTask> logger) : base(logger)
    {
    }

    protected override Task<List<Dictionary<string, string>>> ProcessRecordsAsync(
        List<Dictionary<string, string>> records)
    {
        // 1) Filter columns to only those that actually exist in the CSV
        //    If there's no record, fallback to empty set
        var firstRecord = records.FirstOrDefault();
        var existingHeaders = firstRecord == null
            ? new HashSet<string>()
            : new HashSet<string>(firstRecord.Keys);

        var numericCols = Options.TargetColumns
            .Where(c => existingHeaders.Contains(c))
            .ToArray();

        // If no matching columns exist, just return records as-is
        if (numericCols.Length == 0)
        {
            return Task.FromResult(records);
        }

        // 2) Prepare to gather stats: min, max, mean, stdDev
        var columnStats = new Dictionary<string, (double min, double max, double mean, double stdDev)>();
        var colValuesDict = numericCols.ToDictionary(c => c, c => new List<double>());

        // 3) Parse and collect numeric values
        foreach (var rec in records)
        {
            // ValidateNumericColumns will parse only the columns in numericCols
            // If a value is invalid (including "NaN" after our new parser logic), 
            // we either skip or use DefaultValue, depending on IgnoreErrors.
            if (rec.ValidateNumericColumns(numericCols, out var numericValues,
                Options.IgnoreErrors, Options.DefaultValue))
            {
                // Gather values for stats
                foreach (var col in numericCols)
                {
                    colValuesDict[col].Add(numericValues[col]);
                }
            }
        }

        // 4) Compute stats
        foreach (var col in numericCols)
        {
            var values = colValuesDict[col];
            if (values.Count == 0)
            {
                // No valid data => set stats to 0
                columnStats[col] = (0, 0, 0, 0);
                continue;
            }

            double minVal = values.Min();
            double maxVal = values.Max();
            double meanVal = values.Average();
            double stdVal = 0.0;

            if (values.Count > 1)
            {
                double variance = values.Average(v => Math.Pow(v - meanVal, 2));
                stdVal = Math.Sqrt(variance);
            }

            columnStats[col] = (minVal, maxVal, meanVal, stdVal);
        }

        // 5) Apply normalization
        foreach (var rec in records)
        {
            if (rec.ValidateNumericColumns(numericCols, out var numericValues,
                Options.IgnoreErrors, Options.DefaultValue))
            {
                foreach (var col in numericCols)
                {
                    var stats = columnStats[col];
                    double originalVal = numericValues[col];

                    double newVal;
                    if (Options.Method == NormalizationMethod.MinMax)
                    {
                        double range = stats.max - stats.min;
                        if (Math.Abs(range) < 1e-12)
                        {
                            // All values identical => just set to MinValue
                            newVal = Options.MinValue;
                        }
                        else
                        {
                            double scaled01 = (originalVal - stats.min) / range;
                            newVal = scaled01 * (Options.MaxValue - Options.MinValue)
                                     + Options.MinValue;
                        }
                    }
                    else // ZScore
                    {
                        if (Math.Abs(stats.stdDev) < 1e-12)
                        {
                            // Zero variance => set to mean
                            newVal = stats.mean;
                        }
                        else
                        {
                            newVal = (originalVal - stats.mean) / stats.stdDev;
                        }
                    }

                    // Convert to string
                    rec[col] = NumberText.Format(newVal);
                }
            }
        }

        return Task.FromResult(records);
    }

    protected override IEnumerable<string> GetRequiredColumns()
    {
        return [];
    }
}