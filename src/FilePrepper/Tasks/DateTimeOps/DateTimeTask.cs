using FilePrepper.Utils;
using System.Globalization;
using FilePrepper.Pipeline;

namespace FilePrepper.Tasks.DateTimeOps;

/// <summary>
/// Rewrites a date column in another format, converts Excel serial numbers, or splits a date into numeric features;
/// see <see cref="DateTimeOption"/>. Works on the records directly, without <see cref="DataPipeline"/>.
/// </summary>
public class DateTimeTask : BaseTask<DateTimeOption>
{
    /// <summary>Creates the task.</summary>
    /// <param name="logger">Where progress is logged.</param>
    public DateTimeTask(ILogger<DateTimeTask> logger) : base(logger) { }

    protected override async Task<List<Dictionary<string, string>>> ProcessRecordsAsync(
        List<Dictionary<string, string>> records)
    {
        _logger.LogInformation("Starting DateTime operation: {Mode}", Options.Mode);
        _logger.LogInformation("  Target column: {Column}", Options.Column);

        switch (Options.Mode)
        {
            case DateTimeMode.Parse:
                return await ParseDateTimeAsync(records);

            case DateTimeMode.ParseExcel:
                return await ParseExcelDateAsync(records);

            case DateTimeMode.ExtractFeatures:
                return await ExtractFeaturesAsync(records);

            default:
                throw new ArgumentException($"Unknown DateTime mode: {Options.Mode}");
        }
    }

    private async Task<List<Dictionary<string, string>>> ParseDateTimeAsync(
        List<Dictionary<string, string>> records)
    {
        _logger.LogInformation("  Parsing format: {InputFormat} → {OutputFormat}",
            Options.InputFormat, Options.OutputFormat);

        int successCount = 0;
        int failCount = 0;

        foreach (var record in records)
        {
            if (record.TryGetValue(Options.Column, out var value) && !string.IsNullOrEmpty(value))
            {
                if (DateText.TryParseExact(value, Options.InputFormat!, out var parsedDate))
                {
                    record[Options.Column] = DateText.Format(parsedDate, Options.OutputFormat);
                    successCount++;
                }
                else
                {
                    _logger.LogWarning("Failed to parse DateTime value: {Value}", value);
                    failCount++;
                }
            }
        }

        _logger.LogInformation("✓ Parsed {SuccessCount} records ({FailCount} failed)",
            successCount, failCount);

        return await Task.FromResult(records);
    }

    private async Task<List<Dictionary<string, string>>> ParseExcelDateAsync(
        List<Dictionary<string, string>> records)
    {
        _logger.LogInformation("  Parsing Excel dates → {OutputFormat}", Options.OutputFormat);

        int successCount = 0;
        int failCount = 0;

        foreach (var record in records)
        {
            if (record.TryGetValue(Options.Column, out var value) && !string.IsNullOrEmpty(value))
            {
                if (NumberText.TryParse(value, out var excelDate))
                {
                    try
                    {
                        // Excel dates: 1 = 1900-01-01, add days
                        var baseDate = new DateTime(1899, 12, 30); // Excel epoch
                        var parsedDate = baseDate.AddDays(excelDate);
                        record[Options.Column] = DateText.Format(parsedDate, Options.OutputFormat);
                        successCount++;
                    }
                    catch
                    {
                        _logger.LogWarning("Failed to convert Excel date: {Value}", value);
                        failCount++;
                    }
                }
                else
                {
                    failCount++;
                }
            }
        }

        _logger.LogInformation("✓ Parsed {SuccessCount} Excel dates ({FailCount} failed)",
            successCount, failCount);

        return await Task.FromResult(records);
    }

    private async Task<List<Dictionary<string, string>>> ExtractFeaturesAsync(
        List<Dictionary<string, string>> records)
    {
        var features = DateTimeOption.ParseFeatures(Options.Features);
        var wanted = FeatureColumns(features);
        var featureNames = wanted.Select(feature => feature.Suffix).ToList();

        _logger.LogInformation("  Extracting features: {Features}", string.Join(", ", featureNames));

        int successCount = 0;
        int failCount = 0;

        foreach (var record in records)
        {
            // Every row carries every feature column, empty where its date could not be read: the columns are the command's,
            // not those of whichever row happened to come first.
            foreach (var (suffix, _) in wanted)
                record[$"{Options.Column}_{suffix}"] = string.Empty;

            if (record.TryGetValue(Options.Column, out var value) && !string.IsNullOrEmpty(value))
            {
                if (TryParseDateTime(value, out var parsedDate))
                {
                    foreach (var (suffix, valueOf) in wanted)
                        record[$"{Options.Column}_{suffix}"] = valueOf(parsedDate);

                    successCount++;
                }
                else
                {
                    _logger.LogWarning("Failed to parse DateTime for feature extraction: {Value}", value);
                    failCount++;
                }
            }
        }

        if (Options.RemoveOriginal)
        {
            foreach (var record in records)
            {
                record.Remove(Options.Column);
            }
            _logger.LogInformation("  ✓ Removed original column: {Column}", Options.Column);
        }

        _logger.LogInformation("✓ Extracted {FeatureCount} features from {SuccessCount} records ({FailCount} failed)",
            featureNames.Count, successCount, failCount);

        return await Task.FromResult(records);
    }

    /// <summary>The columns each feature makes, in the order they are written, and how a date gives their value.</summary>
    private static List<(string Suffix, Func<DateTime, string> Value)> FeatureColumns(DateFeatures features)
    {
        var all = new (DateFeatures Flag, string Suffix, Func<DateTime, string> Value)[]
        {
            (DateFeatures.Year, "Year", date => date.Year.ToString()),
            (DateFeatures.Month, "Month", date => date.Month.ToString()),
            (DateFeatures.Day, "Day", date => date.Day.ToString()),
            (DateFeatures.Hour, "Hour", date => date.Hour.ToString()),
            (DateFeatures.Minute, "Minute", date => date.Minute.ToString()),
            (DateFeatures.DayOfWeek, "DayOfWeek", date => ((int)date.DayOfWeek).ToString()),
            (DateFeatures.DayOfYear, "DayOfYear", date => date.DayOfYear.ToString()),
            (DateFeatures.WeekOfYear, "WeekOfYear", date => DateText.WeekOfYear(date).ToString(CultureInfo.InvariantCulture)),
            (DateFeatures.Quarter, "Quarter", date => ((date.Month - 1) / 3 + 1).ToString()),
        };
        return [.. all.Where(feature => features.HasFlag(feature.Flag)).Select(feature => (feature.Suffix, feature.Value))];
    }

    private bool TryParseDateTime(string value, out DateTime result)
    {
        // Try multiple common formats
        var formats = new[]
        {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-dd",
            "yyyy/MM/dd HH:mm:ss",
            "yyyy/MM/dd HH:mm",
            "yyyy/MM/dd",
            "MM/dd/yyyy HH:mm:ss",
            "MM/dd/yyyy HH:mm",
            "MM/dd/yyyy",
            "yyyyMMddHHmmss",
            "yyyyMMddHHmm",
            "yyyyMMdd",
            "yyyy-MM-dd H:mm",  // Dataset 002 format
            "M/d/yyyy H:mm"
        };

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(
                value,
                format,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out result))
            {
                return true;
            }
        }

        // Try general parsing as fallback
        if (DateText.TryParse(value, out result))
        {
            return true;
        }

        result = default;
        return false;
    }

    protected override IEnumerable<string> GetRequiredColumns() => new[] { Options.Column };

    protected override string[] ValidateTaskSpecific(TaskContext context)
    {
        var errors = new List<string>();

        // Column existence will be validated by GetRequiredColumns
        // Additional mode-specific validation
        if (Options.Mode == DateTimeMode.Parse && string.IsNullOrWhiteSpace(Options.InputFormat))
        {
            errors.Add("InputFormat is required for Parse mode");
        }

        if (Options.Mode == DateTimeMode.ExtractFeatures && string.IsNullOrWhiteSpace(Options.Features))
        {
            errors.Add("Features are required for ExtractFeatures mode");
        }

        return errors.ToArray();
    }
}
