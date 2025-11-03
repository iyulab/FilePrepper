using System.CommandLine;
using System.Text;
using FilePrepper.Tasks;
using FilePrepper.Tasks.FileFormatConvert;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

public class FileFormatConvertCommand : BaseCommand
{
    private readonly Option<string> _inputOption;
    private readonly Option<string> _outputOption;
    private readonly Option<string> _targetOption;
    private readonly Option<string> _encodingOption;
    private readonly Option<bool> _prettyOption;
    private readonly Option<string> _rootOption;
    private readonly Option<string> _itemOption;

    public FileFormatConvertCommand(ILoggerFactory loggerFactory)
        : base("convert-format", "Convert file format", loggerFactory)
    {
        _inputOption = new Option<string>(new[] { "--input", "-i" }, "Input file path") { IsRequired = true };
        _outputOption = new Option<string>(new[] { "--output", "-o" }, "Output file path") { IsRequired = true };
        _targetOption = new Option<string>(new[] { "--target", "-t" }, "Target format (CSV/TSV/PSV/JSON/XML)") { IsRequired = true };
        _encodingOption = new Option<string>(new[] { "--encoding", "-e" }, () => "utf-8", "File encoding");
        _prettyOption = new Option<bool>("--pretty", () => false, "Pretty print for JSON/XML");
        _rootOption = new Option<string>("--root", () => "root", "Root element name for XML");
        _itemOption = new Option<string>("--item", () => "item", "Item element name for XML");

        AddOption(_inputOption);
        AddOption(_outputOption);
        AddOption(_targetOption);
        AddOption(_encodingOption);
        AddOption(_prettyOption);
        AddOption(_rootOption);
        AddOption(_itemOption);

        this.SetHandler(async (context) =>
        {
            context.ExitCode = await ExecuteAsync(
                context.ParseResult.GetValueForOption(_inputOption)!,
                context.ParseResult.GetValueForOption(_outputOption)!,
                context.ParseResult.GetValueForOption(_targetOption)!,
                context.ParseResult.GetValueForOption(_encodingOption)!,
                context.ParseResult.GetValueForOption(_prettyOption),
                context.ParseResult.GetValueForOption(_rootOption)!,
                context.ParseResult.GetValueForOption(_itemOption)!,
                context.ParseResult.GetValueForOption(CommonOptions.HasHeader),
                context.ParseResult.GetValueForOption(CommonOptions.IgnoreErrors),
                context.ParseResult.GetValueForOption(CommonOptions.Verbose));
        });
    }

    private async Task<int> ExecuteAsync(string inputPath, string outputPath, string targetStr, string encodingStr,
        bool pretty, string root, string item, bool hasHeader, bool ignoreErrors, bool verbose)
    {
        try
        {
            if (!Enum.TryParse<FileFormat>(targetStr, true, out var format))
            {
                DisplayError($"Invalid target format: {targetStr}");
                return ExitCodes.InvalidArguments;
            }

            var encoding = Encoding.GetEncoding(encodingStr);

            var options = new FileFormatConvertOption
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                TargetFormat = format,
                Encoding = encoding,
                PrettyPrint = pretty,
                RootElementName = root,
                ItemElementName = item,
                HasHeader = hasHeader,
                IgnoreErrors = ignoreErrors
            };

            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Converting format...", async ctx =>
                {
                    var taskLogger = LoggerFactory.CreateLogger<FileFormatConvertTask>();
                    var task = new FileFormatConvertTask(taskLogger);
                    var taskContext = new TaskContext(options);

                    var success = await task.ExecuteAsync(taskContext);
                    if (success)
                    {
                        DisplaySuccess($"Format converted: {outputPath}");
                        return ExitCodes.Success;
                    }

                    DisplayError("Failed to convert format");
                    return ExitCodes.Error;
                });
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }
}
