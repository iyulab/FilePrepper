using System.CommandLine;
using FilePrepper.Tasks;
using FilePrepper.Tasks.OneHotEncoding;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

public class OneHotEncodingCommand : BaseCommand
{
    private readonly Option<string> _inputOption;
    private readonly Option<string> _outputOption;
    private readonly Option<string[]> _columnsOption;
    private readonly Option<bool> _dropFirstOption;
    private readonly Option<bool> _keepOriginalOption;

    public OneHotEncodingCommand(ILoggerFactory loggerFactory)
        : base("one-hot-encoding", "Perform one-hot encoding on categorical columns", loggerFactory)
    {
        _inputOption = new Option<string>("--input", new[] { "-i" }) { Description = "Input file path", Required = true };
        _outputOption = new Option<string>("--output", new[] { "-o" }) { Description = "Output file path", Required = true };
        _columnsOption = new Option<string[]>("--columns", new[] { "-c" }) { Description = "Columns to encode", CustomParser = result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries), Required = true };
        _dropFirstOption = new Option<bool>("--drop-first") { Description = "Drop first category", DefaultValueFactory = _ => false };
        _keepOriginalOption = new Option<bool>("--keep-original") { Description = "Keep original columns", DefaultValueFactory = _ => false };

        Add(_inputOption);
        Add(_outputOption);
        Add(_columnsOption);
        Add(_dropFirstOption);
        Add(_keepOriginalOption);

        this.SetAction(async (parseResult) =>
        {
            return await ExecuteAsync(
                parseResult.GetValue(_inputOption)!,
                parseResult.GetValue(_outputOption)!,
                parseResult.GetValue(_columnsOption)!,
                parseResult.GetValue(_dropFirstOption),
                parseResult.GetValue(_keepOriginalOption),
                parseResult.GetValue(CommonOptions.HasHeader),
                parseResult.GetValue(CommonOptions.IgnoreErrors),
                parseResult.GetValue(CommonOptions.Verbose));
        });
    }

    private async Task<int> ExecuteAsync(string inputPath, string outputPath, string[] columns,
        bool dropFirst, bool keepOriginal, bool hasHeader, bool ignoreErrors, bool verbose)
    {
        try
        {
            var options = new OneHotEncodingOption
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                TargetColumns = columns,
                DropFirst = dropFirst,
                KeepOriginalColumns = keepOriginal,
                HasHeader = hasHeader,
                IgnoreErrors = ignoreErrors
            };

            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Performing one-hot encoding...", async ctx =>
                {
                    var taskLogger = LoggerFactory.CreateLogger<OneHotEncodingTask>();
                    var task = new OneHotEncodingTask(taskLogger);
                    var taskContext = new TaskContext(options);

                    var success = await task.ExecuteAsync(taskContext);
                    if (success)
                    {
                        DisplaySuccess($"One-hot encoding complete: {outputPath}");
                        return ExitCodes.Success;
                    }

                    DisplayError("Failed to perform one-hot encoding");
                    return ExitCodes.Error;
                });
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }
}
