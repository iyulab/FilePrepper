using CommandLine;
using FilePrepper.Tasks.FileFormatConvert;
using Microsoft.Extensions.Logging;

namespace FilePrepper.CLI.Tools.FileFormatConvert;

[Verb("convert-format", HelpText = "Convert file format")]
public class FileFormatConvertParameters : SingleInputParameters
{
    [Option('t', "target", Required = true,
        HelpText = "Target format (CSV/TSV/PSV/JSON/XML)")]
    public string TargetFormat { get; set; } = string.Empty;

    [Option('e', "output-encoding", Default = "utf-8",
        HelpText = "Output file encoding")]
    public string OutputEncoding { get; set; } = "utf-8";

    [Option("pretty", Default = false,
        HelpText = "Enable pretty printing for JSON/XML")]
    public bool PrettyPrint { get; set; }

    [Option("root", Default = "root",
        HelpText = "Root element name for XML format")]
    public string RootElementName { get; set; } = "root";

    [Option("item", Default = "item",
        HelpText = "Item element name for XML format")]
    public string ItemElementName { get; set; } = "item";

    public override Type GetHandlerType() => typeof(FileFormatConvertHandler);

    protected override bool ValidateInternal(ILogger logger)
    {
        if (!base.ValidateInternal(logger))
            return false;

        if (!Enum.TryParse<FileFormat>(TargetFormat, true, out var format))
        {
            logger.LogError("Invalid target format: {Format}. Valid values are: {ValidValues}",
                TargetFormat, string.Join(", ", Enum.GetNames<FileFormat>()));
            return false;
        }

        // 인코딩 유효성 검사
        try
        {
            _ = System.Text.Encoding.GetEncoding(OutputEncoding);
        }
        catch (ArgumentException)
        {
            logger.LogError("Invalid encoding: {Encoding}", OutputEncoding);
            return false;
        }

        // XML 포맷 관련 추가 검증
        if (format == FileFormat.XML)
        {
            if (string.IsNullOrWhiteSpace(RootElementName))
            {
                logger.LogError("Root element name cannot be empty for XML format");
                return false;
            }
            if (string.IsNullOrWhiteSpace(ItemElementName))
            {
                logger.LogError("Item element name cannot be empty for XML format");
                return false;
            }
        }

        return true;
    }

    public override string? GetExample() =>
        "convert-format -i input.csv -o output.json -t JSON --pretty";
}