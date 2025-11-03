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
        _inputOption = new Option<string>(new[] { "--input", "-i" }, "Input file path") { IsRequired = true };
        _outputOption = new Option<string>(new[] { "--output", "-o" }, "Output file path") { IsRequired = true };
        _columnsOption = new Option<string[]>(
            aliases: new[] { "--columns", "-c" },
            description: "Columns to encode",
            parseArgument: result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries)) { IsRequired = true };
        _dropFirstOption = new Option<bool>("--drop-first", () => false, "Drop first category");
        _keepOriginalOption = new Option<bool>("--keep-original", () => false, "Keep original columns");

        AddOption(_inputOption);
        AddOption(_outputOption);
        AddOption(_columnsOption);
        AddOption(_dropFirstOption);
        AddOption(_keepOriginalOption);

        this.SetHandler(async (context) =>
        {
            context.ExitCode = await ExecuteAsync(
                context.ParseResult.GetValueForOption(_inputOption)!,
                context.ParseResult.GetValueForOption(_outputOption)!,
                context.ParseResult.GetValueForOption(_columnsOption)!,
                context.ParseResult.GetValueForOption(_dropFirstOption),
                context.ParseResult.GetValueForOption(_keepOriginalOption),
                context.ParseResult.GetValueForOption(CommonOptions.HasHeader),
                context.ParseResult.GetValueForOption(CommonOptions.IgnoreErrors),
                context.ParseResult.GetValueForOption(CommonOptions.Verbose));
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
