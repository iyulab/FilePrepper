using System.Globalization;
using FilePrepper.Pipeline;

namespace FilePrepper.Tasks.DateTimeOps;

/// <summary>
/// High-performance DateTime parsing and feature extraction task
/// Directly manipulates records without DataPipeline overhead
/// </summary>
public class DateTimeTask : BaseTask<DateTimeOption>
{
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
                if (DateTime.TryParseExact(
                    value,
                    Options.InputFormat!,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsedDate))
                {
                    record[Options.Column] = parsedDate.ToString(Options.OutputFormat);
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
                if (double.TryParse(value, out var excelDate))
                {
                    try
                    {
                        // Excel dates: 1 = 1900-01-01, add days
                        var baseDate = new DateTime(1899, 12, 30); // Excel epoch
                        var parsedDate = baseDate.AddDays(excelDate);
                        record[Options.Column] = parsedDate.ToString(Options.OutputFormat);
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
        var features = ParseDateFeatures(Options.Features!);
        var featureNames = GetFeatureNames(features);

        _logger.LogInformation("  Extracting features: {Features}", string.Join(", ", featureNames));

        int successCount = 0;
        int failCount = 0;

        foreach (var record in records)
        {
            if (record.TryGetValue(Options.Column, out var value) && !string.IsNullOrEmpty(value))
            {
                if (TryParseDateTime(value, out var parsedDate))
                {
                    // Extract features
                    if (features.HasFlag(DateFeatures.Year))
                        record[$"{Options.Column}_Year"] = parsedDate.Year.ToString();

                    if (features.HasFlag(DateFeatures.Month))
                        record[$"{Options.Column}_Month"] = parsedDate.Month.ToString();

                    if (features.HasFlag(DateFeatures.Day))
                        record[$"{Options.Column}_Day"] = parsedDate.Day.ToString();

                    if (features.HasFlag(DateFeatures.Hour))
                        record[$"{Options.Column}_Hour"] = parsedDate.Hour.ToString();

                    if (features.HasFlag(DateFeatures.Minute))
                        record[$"{Options.Column}_Minute"] = parsedDate.Minute.ToString();

                    if (features.HasFlag(DateFeatures.DayOfWeek))
                        record[$"{Options.Column}_DayOfWeek"] = ((int)parsedDate.DayOfWeek).ToString();

                    if (features.HasFlag(DateFeatures.DayOfYear))
                        record[$"{Options.Column}_DayOfYear"] = parsedDate.DayOfYear.ToString();

                    if (features.HasFlag(DateFeatures.WeekOfYear))
                    {
                        var week = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                            parsedDate,
                            CalendarWeekRule.FirstFourDayWeek,
                            DayOfWeek.Monday);
                        record[$"{Options.Column}_WeekOfYear"] = week.ToString();
                    }

                    if (features.HasFlag(DateFeatures.Quarter))
                        record[$"{Options.Column}_Quarter"] = ((parsedDate.Month - 1) / 3 + 1).ToString();

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
        if (DateTime.TryParse(value, out result))
        {
            return true;
        }

        result = default;
        return false;
    }

    private DateFeatures ParseDateFeatures(string featuresStr)
    {
        var result = DateFeatures.None;
        var parts = featuresStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            if (Enum.TryParse<DateFeatures>(part, ignoreCase: true, out var feature))
            {
                result |= feature;
            }
            else
            {
                _logger.LogWarning("Unknown feature: {Feature}, skipping", part);
            }
        }

        return result;
    }

    private List<string> GetFeatureNames(DateFeatures features)
    {
        var names = new List<string>();

        if (features.HasFlag(DateFeatures.Year)) names.Add("Year");
        if (features.HasFlag(DateFeatures.Month)) names.Add("Month");
        if (features.HasFlag(DateFeatures.Day)) names.Add("Day");
        if (features.HasFlag(DateFeatures.Hour)) names.Add("Hour");
        if (features.HasFlag(DateFeatures.Minute)) names.Add("Minute");
        if (features.HasFlag(DateFeatures.DayOfWeek)) names.Add("DayOfWeek");
        if (features.HasFlag(DateFeatures.DayOfYear)) names.Add("DayOfYear");
        if (features.HasFlag(DateFeatures.WeekOfYear)) names.Add("WeekOfYear");
        if (features.HasFlag(DateFeatures.Quarter)) names.Add("Quarter");

        return names;
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
