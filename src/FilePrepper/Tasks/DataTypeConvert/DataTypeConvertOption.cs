using FilePrepper.Utils;
namespace FilePrepper.Tasks.DataTypeConvert;

public enum DataType
{
    String,
    Integer,
    Decimal,
    DateTime,
    Boolean
}

public class ColumnTypeConversion
{
    public string ColumnName { get; set; } = string.Empty;
    public DataType TargetType { get; set; }
    public string? DateTimeFormat { get; set; }
    public string? DefaultValue { get; set; }
    public CultureInfo? Culture { get; set; }
    public bool TrimWhitespace { get; set; } = true;
    public bool IgnoreCase { get; set; } = true;
}

public class DataTypeConvertOption : SingleInputOption, IDefaultValueOption
{
    private static readonly string[] TrueValues = ["true", "yes", "1", "y", "t"];
    private static readonly string[] FalseValues = ["false", "no", "0", "n", "f"];

    public List<ColumnTypeConversion> Conversions { get; set; } = new();
    public string? DefaultValue { get; set; }

    protected override string[] ValidateInternal()
    {
        var errors = new List<string>();

        if (Conversions == null || Conversions.Count == 0)
        {
            errors.Add("At least one column conversion must be specified");
            return [.. errors];
        }

        foreach (var conversion in Conversions)
        {
            if (string.IsNullOrWhiteSpace(conversion.ColumnName))
            {
                errors.Add("Column name cannot be empty or whitespace");
            }

            if (conversion.TargetType == DataType.DateTime &&
                string.IsNullOrWhiteSpace(conversion.DateTimeFormat))
            {
                errors.Add($"DateTime format must be specified for column {conversion.ColumnName}");
            }

            if (conversion.DefaultValue != null)
            {
                if (!IsValidDefaultValue(conversion.DefaultValue, conversion.TargetType))
                {
                    errors.Add($"Invalid default value for {conversion.TargetType}: {conversion.DefaultValue}");
                }
            }
        }

        return [.. errors];
    }

    private bool IsValidDefaultValue(string value, DataType targetType)
    {
        try
        {
            return targetType switch
            {
                DataType.Integer => int.TryParse(value, out _),
                DataType.Decimal => NumberText.TryParseDecimal(value, out _),
                DataType.DateTime => DateTime.TryParse(value, out _),
                DataType.Boolean => IsValidBooleanValue(value),
                DataType.String => true,
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }

    private bool IsValidBooleanValue(string value)
    {
        return TrueValues.Contains(value.ToLowerInvariant()) ||
               FalseValues.Contains(value.ToLowerInvariant()) ||
               bool.TryParse(value, out _);
    }
}