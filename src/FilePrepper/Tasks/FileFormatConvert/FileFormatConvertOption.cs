namespace FilePrepper.Tasks.FileFormatConvert;

public enum FileFormat
{
    CSV,
    TSV,
    PSV, // Pipe Separated Values
    JSON,
    XML
}

public class FileFormatConvertOption : SingleInputOption
{
    public FileFormat TargetFormat { get; set; }
    public Encoding? Encoding { get; set; }
    public string? Delimiter { get; set; }
    public new bool HasHeader { get; set; } = true;
    public string? DateTimeFormat { get; set; }
    public bool PrettyPrint { get; set; } = false;
    public string RootElementName { get; set; } = "root";
    public string ItemElementName { get; set; } = "item";

    protected override string[] ValidateInternal()
    {
        var errors = new List<string>();

        if (TargetFormat == FileFormat.CSV && !string.IsNullOrEmpty(Delimiter))
        {
            errors.Add("Delimiter cannot be specified for CSV format");
        }

        if (TargetFormat == FileFormat.TSV && !string.IsNullOrEmpty(Delimiter))
        {
            errors.Add("Delimiter cannot be specified for TSV format");
        }

        if (TargetFormat == FileFormat.PSV && !string.IsNullOrEmpty(Delimiter))
        {
            errors.Add("Delimiter cannot be specified for PSV format");
        }

        if (TargetFormat == FileFormat.XML)
        {
            errors.AddRange(ValidationUtils.ValidateColumns(new[] { RootElementName }, "root element name"));
            errors.AddRange(ValidationUtils.ValidateColumns(new[] { ItemElementName }, "item element name"));
        }

        return [.. errors];
    }
}