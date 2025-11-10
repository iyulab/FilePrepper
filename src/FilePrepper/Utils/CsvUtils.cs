using CsvHelper;
using CsvHelper.Configuration;

namespace FilePrepper.Utils;

public static class CsvUtils
{
    public static CsvConfiguration GetDefaultConfiguration(bool hasHeader = true)
    {
        return new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = hasHeader,
            MissingFieldFound = null
        };
    }

    public static List<string> ValidateHeaders(IEnumerable<string> requiredColumns, IEnumerable<string> actualHeaders)
    {
        var errors = new List<string>();
        var headerSet = new HashSet<string>(actualHeaders);

        foreach (var column in requiredColumns)
        {
            if (!headerSet.Contains(column))
            {
                errors.Add($"Required column not found: {column}");
            }
        }

        return errors;
    }

    /// <summary>
    /// Parse string to double, but reject NaN/Infinity as invalid.
    /// Automatically cleans comma-formatted numbers (e.g., "1,000" → 1000).
    /// </summary>
    public static bool TryParseNumeric(string? input, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        // Clean comma-formatted numbers: "1,000", "2,000.5"
        var cleaned = CleanNumericString(input);

        if (double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            // If parsed but is NaN or Infinity, treat as invalid
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return false;
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// Cleans numeric strings by removing thousand separators (commas).
    /// Examples: "1,000" → "1000", "2,000.5" → "2000.5"
    /// </summary>
    public static string CleanNumericString(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var trimmed = input.Trim();

        // Detect comma-formatted numbers: "1,000", "2,000.5", "-1,000"
        // Pattern: optional sign, digits with optional commas, optional decimal part
        if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^-?[\d,]+\.?\d*$"))
        {
            return trimmed.Replace(",", "");
        }

        return trimmed;
    }

    public static Dictionary<string, double> ParseNumericColumns(
        Dictionary<string, string> record,
        string[] columns,
        bool ignoreErrors = false,
        double? defaultValue = null)
    {
        var result = new Dictionary<string, double>();
        foreach (var col in columns)
        {
            if (!record.TryGetValue(col, out string? value))
            {
                if (ignoreErrors && defaultValue.HasValue)
                {
                    result[col] = defaultValue.Value;
                    continue;
                }
                throw new KeyNotFoundException($"Column not found: {col}");
            }

            if (TryParseNumeric(value, out var v))
            {
                result[col] = v;
            }
            else if (ignoreErrors && defaultValue.HasValue)
            {
                result[col] = defaultValue.Value;
            }
            else
            {
                throw new ArgumentException($"Invalid numeric value in column {col}: {record[col]}");
            }
        }
        return result;
    }

    public static bool ValidateNumericColumns(
        this Dictionary<string, string> record,
        IEnumerable<string> numericColumns,
        out Dictionary<string, double> numericValues,
        bool ignoreErrors = false,
        string? defaultValue = null)
    {
        numericValues = [];

        foreach (var column in numericColumns)
        {
            if (!record.TryGetValue(column, out string? value))
            {
                if (ignoreErrors && defaultValue != null)
                {
                    if (double.TryParse(defaultValue, out var defaultNum))
                    {
                        numericValues[column] = defaultNum;
                        continue;
                    }
                }
                return false;
            }

            if (!CsvUtils.TryParseNumeric(value, out var v2))
            {
                if (ignoreErrors && defaultValue != null)
                {
                    if (double.TryParse(defaultValue, out var defaultNum))
                    {
                        numericValues[column] = defaultNum;
                        continue;
                    }
                }
                return false;
            }

            numericValues[column] = v2;
        }

        return true;
    }
}