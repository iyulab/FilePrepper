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
        _inputOption = new Option<string>("--input", new[] { "-i" }) { Description = "Input file path", Required = true };
        _outputOption = new Option<string>("--output", new[] { "-o" }) { Description = "Output file path", Required = true };
        _targetOption = new Option<string>("--target", new[] { "-t" }) { Description = "Target format (CSV/TSV/PSV/JSON/XML)", Required = true };
        _encodingOption = new Option<string>("--encoding", new[] { "-e" }) { Description = "File encoding", DefaultValueFactory = _ => "utf-8" };
        _prettyOption = new Option<bool>("--pretty") { Description = "Pretty print for JSON/XML", DefaultValueFactory = _ => false };
        _rootOption = new Option<string>("--root") { Description = "Root element name for XML", DefaultValueFactory = _ => "root" };
        _itemOption = new Option<string>("--item") { Description = "Item element name for XML", DefaultValueFactory = _ => "item" };

        Add(_inputOption);
        Add(_outputOption);
        Add(_targetOption);
        Add(_encodingOption);
        Add(_prettyOption);
        Add(_rootOption);
        Add(_itemOption);

        this.SetAction(async (parseResult) =>
        {
            return await ExecuteAsync(
                parseResult.GetValue(_inputOption)!,
                parseResult.GetValue(_outputOption)!,
                parseResult.GetValue(_targetOption)!,
                parseResult.GetValue(_encodingOption)!,
                parseResult.GetValue(_prettyOption),
                parseResult.GetValue(_rootOption)!,
                parseResult.GetValue(_itemOption)!,
                parseResult.GetValue(CommonOptions.HasHeader),
                parseResult.GetValue(CommonOptions.IgnoreErrors),
                parseResult.GetValue(CommonOptions.Verbose));
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
