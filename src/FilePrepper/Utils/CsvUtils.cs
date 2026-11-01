using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace FilePrepper.Utils;

public static class CsvUtils
{
    /// <summary>
    /// Creates a StreamReader with the appropriate encoding.
    /// When encoding is "auto", auto-detects the file encoding.
    /// </summary>
    public static StreamReader CreateReader(string path, string encoding = "auto")
    {
        var enc = EncodingDetector.ResolveEncoding(path, encoding);
        return new StreamReader(path, enc);
    }

    public static CsvConfiguration GetDefaultConfiguration(bool hasHeader = true, string delimiter = ",")
    {
        return new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = hasHeader,
            Delimiter = delimiter,
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
    /// A cell read as a number — <see cref="NumberText.TryParse"/>, the one reader: finite, no separators. This used to strip every
    /// comma first, so <c>1,5</c> was 15 and <c>12,34</c> was 1234 in the commands that called it (aggregate, stats, column-interaction)
    /// and not a number in the others.
    /// </summary>
    public static bool TryParseNumeric(string? input, out double value)
    {
        value = 0;
        return !string.IsNullOrWhiteSpace(input) && NumberText.TryParse(input, out value);
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
                    if (NumberText.TryParse(defaultValue, out var defaultNum))
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
                    if (NumberText.TryParse(defaultValue, out var defaultNum))
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