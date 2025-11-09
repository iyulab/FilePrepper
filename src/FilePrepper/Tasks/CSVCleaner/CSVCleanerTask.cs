using Microsoft.Extensions.Logging;
using System.Globalization;

namespace FilePrepper.Tasks.CSVCleaner;

/// <summary>
/// Task to clean CSV numeric data by removing thousand separators
/// </summary>
public class CSVCleanerTask : BaseTask<CSVCleanerOption>
{
    public CSVCleanerTask(ILogger<CSVCleanerTask> logger) : base(logger)
    {
    }

    protected override Task<List<Dictionary<string, string>>> ProcessRecordsAsync(
        List<Dictionary<string, string>> records)
    {
        _logger.LogInformation("Cleaning {Count} records", records.Count);

        if (records.Count == 0)
        {
            _logger.LogWarning("No records to process");
            return Task.FromResult(records);
        }

        // Determine which columns to clean
        var columnsToClean = Options.TargetColumns.Count > 0
            ? Options.TargetColumns
            : _originalHeaders;

        _logger.LogInformation("Cleaning {Count} column(s)", columnsToClean.Count);

        // Track cleaning statistics
        int totalCleaned = 0;
        int totalErrors = 0;

        var processedRecords = records.Select(record =>
        {
            var cleanedRecord = new Dictionary<string, string>();

            foreach (var column in _originalHeaders)
            {
                var value = record[column];

                if (columnsToClean.Contains(column))
                {
                    var cleanedValue = CleanNumericValue(value, out bool wasCleaned, out bool hasError);

                    if (wasCleaned)
                        totalCleaned++;

                    if (hasError && !Options.IgnoreErrors)
                    {
                        totalErrors++;
                        throw new InvalidOperationException(
                            $"Invalid numeric value '{value}' in column '{column}' after cleaning");
                    }

                    cleanedRecord[column] = cleanedValue;
                }
                else
                {
                    cleanedRecord[column] = value;
                }
            }

            return cleanedRecord;
        }).ToList();

        _logger.LogInformation("Cleaning completed: {Cleaned} values cleaned, {Errors} validation errors",
            totalCleaned, totalErrors);

        return Task.FromResult(processedRecords);
    }

    private string CleanNumericValue(string value, out bool wasCleaned, out bool hasError)
    {
        wasCleaned = false;
        hasError = false;

        if (string.IsNullOrWhiteSpace(value))
            return value;

        var originalValue = value;
        var cleaned = value;

        // Remove thousand separators
        if (cleaned.Contains(Options.ThousandSeparator))
        {
            cleaned = cleaned.Replace(Options.ThousandSeparator.ToString(), "");
            wasCleaned = true;
        }

        // Remove whitespace if requested
        if (Options.RemoveWhitespace && cleaned.Contains(' '))
        {
            cleaned = cleaned.Replace(" ", "");
            wasCleaned = true;
        }

        // Validate if requested
        if (Options.ValidateNumeric && wasCleaned)
        {
            // Try to parse as double to validate
            if (!double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            {
                hasError = true;
                _logger.LogWarning("Value '{Original}' cleaned to '{Cleaned}' is not a valid number",
                    originalValue, cleaned);
            }
        }

        return cleaned;
    }
}
