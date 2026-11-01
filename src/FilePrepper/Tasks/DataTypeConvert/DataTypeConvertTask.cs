using FilePrepper.Tasks.DataTypeConvert;
using FilePrepper.Tasks;
using FilePrepper;

public class DataTypeConvertTask : BaseTask<DataTypeConvertOption>
{
    private static readonly string[] TrueValues = ["true", "yes", "1", "y", "t"];
    private static readonly string[] FalseValues = ["false", "no", "0", "n", "f"];

    public DataTypeConvertTask(ILogger<DataTypeConvertTask> logger) : base(logger)
    {
    }

    protected override async Task<List<Dictionary<string, string>>> ProcessRecordsAsync(
        List<Dictionary<string, string>> records)
    {
        await Task.Run(() =>
        {
            foreach (var record in records)
            {
                foreach (var conversion in Options.Conversions)
                {
                    try
                    {
                        record[conversion.ColumnName] = ConvertValue(
                            record[conversion.ColumnName],
                            conversion);
                    }
                    catch (ValidationException) when (Options.IgnoreErrors)
                    {
                        record[conversion.ColumnName] = conversion.DefaultValue ?? string.Empty;
                    }
                    catch (Exception ex) when (Options.IgnoreErrors)
                    {
                        record[conversion.ColumnName] = conversion.DefaultValue ?? string.Empty;
                        _logger.LogWarning("Error converting value: {Error}", ex.Message);
                    }
                }
            }
        });

        return records;
    }

    private string ConvertValue(string value, ColumnTypeConversion conversion)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return conversion.DefaultValue ?? string.Empty;
        }

        var culture = conversion.Culture ?? CultureInfo.InvariantCulture;
        string processedValue = conversion.TrimWhitespace ? value.Trim() : value;

        try
        {
            return conversion.TargetType switch
            {
                DataType.Integer => ConvertToInteger(processedValue, culture).ToString(culture),
                DataType.Decimal => ConvertToDecimal(processedValue, culture),
                DataType.DateTime => ConvertToDateTime(processedValue, conversion, culture),
                DataType.Boolean => ConvertToBoolean(processedValue, conversion.IgnoreCase).ToString(),
                DataType.String => processedValue,
                _ => throw new ValidationException($"Unsupported target type: {conversion.TargetType}",
                    ValidationExceptionErrorCode.General)
            };
        }
        catch (Exception ex) when (ex is not ValidationException)
        {
            var error = $"Error converting value '{value}' to {conversion.TargetType}: {ex.Message}";
            _logger.LogWarning(error);
            throw new ValidationException(error, ValidationExceptionErrorCode.General);
        }
    }

    private int ConvertToInteger(string value, CultureInfo culture)
    {
        try
        {
            // 소수점이 있는 경우 반올림
            if (value.Contains(".") || value.Contains(","))
            {
                return (int)Math.Round(decimal.Parse(value, culture));
            }
            return int.Parse(value, culture);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            throw new ValidationException($"Invalid integer value: {value}", ValidationExceptionErrorCode.General);
        }
    }

    private string ConvertToDecimal(string value, CultureInfo culture)
    {
        try
        {
            var styles = NumberStyles.Number | NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands;
            var decimalValue = decimal.Parse(value, styles, culture);

            // 원본 소수점 자릿수 유지
            var originalDecimals = 1; // 최소 1자리
            foreach (var separator in new[] {
                culture.NumberFormat.NumberDecimalSeparator,
                ".",
                ","
            })
            {
                if (value.Contains(separator))
                {
                    var parts = value.Split(separator);
                    if (parts.Length > 1)
                    {
                        originalDecimals = Math.Max(originalDecimals, parts[1].Trim().Length);
                        break;
                    }
                }
            }

            return decimalValue.ToString($"F{originalDecimals}", CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            throw new ValidationException($"Invalid decimal value: {value}", ValidationExceptionErrorCode.General);
        }
    }

    private string ConvertToDateTime(string value, ColumnTypeConversion conversion, CultureInfo culture)
    {
        try
        {
            if (!DateText.TryParse(value, culture, out var dateTime))
                throw new ValidationException($"Invalid date time value: {value}", ValidationExceptionErrorCode.General);

            return dateTime.ToString(conversion.DateTimeFormat, culture);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            throw new ValidationException($"Invalid date time value: {value}", ValidationExceptionErrorCode.General);
        }
    }

    private bool ConvertToBoolean(string value, bool ignoreCase)
    {
        try
        {
            if (TrueValues.Contains(value, StringComparer.Create(CultureInfo.InvariantCulture, ignoreCase)))
            {
                return true;
            }
            if (FalseValues.Contains(value, StringComparer.Create(CultureInfo.InvariantCulture, ignoreCase)))
            {
                return false;
            }

            return bool.Parse(value);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            throw new ValidationException($"Invalid boolean value: {value}", ValidationExceptionErrorCode.General);
        }
    }

    protected override IEnumerable<string> GetRequiredColumns()
    {
        return Options.Conversions.Select(c => c.ColumnName);
    }
}