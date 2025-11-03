using System.CommandLine;
using FilePrepper.Tasks;
using FilePrepper.Tasks.RemoveColumns;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

public class RemoveColumnsCommand : BaseCommand
{
    private readonly Option<string> _inputOption;
    private readonly Option<string> _outputOption;
    private readonly Option<string[]> _columnsOption;

    public RemoveColumnsCommand(ILoggerFactory loggerFactory)
        : base("remove-columns", "Remove specified columns from the input file", loggerFactory)
    {
        _inputOption = new Option<string>(new[] { "--input", "-i" }, "Input file path") { IsRequired = true };
        _outputOption = new Option<string>(new[] { "--output", "-o" }, "Output file path") { IsRequired = true };
        _columnsOption = new Option<string[]>(
            aliases: new[] { "--columns", "-c" },
            description: "Columns to remove",
            parseArgument: result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries)) { IsRequired = true };

        AddOption(_inputOption);
        AddOption(_outputOption);
        AddOption(_columnsOption);

        this.SetHandler(async (context) =>
        {
            context.ExitCode = await ExecuteAsync(
                context.ParseResult.GetValueForOption(_inputOption)!,
                context.ParseResult.GetValueForOption(_outputOption)!,
                context.ParseResult.GetValueForOption(_columnsOption)!,
                context.ParseResult.GetValueForOption(CommonOptions.HasHeader),
                context.ParseResult.GetValueForOption(CommonOptions.IgnoreErrors),
                context.ParseResult.GetValueForOption(CommonOptions.Verbose));
        });
    }

    private async Task<int> ExecuteAsync(string inputPath, string outputPath, string[] columns,
        bool hasHeader, bool ignoreErrors, bool verbose)
    {
        try
        {
            var options = new RemoveColumnsOption
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                RemoveColumns = columns.ToList(),
                HasHeader = hasHeader,
                IgnoreErrors = ignoreErrors
            };

            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Removing columns...", async ctx =>
                {
                    var taskLogger = LoggerFactory.CreateLogger<RemoveColumnsTask>();
                    var task = new RemoveColumnsTask(taskLogger);
                    var taskContext = new TaskContext(options);

                    var success = await task.ExecuteAsync(taskContext);
                    if (success)
                    {
                        DisplaySuccess($"Columns removed: {outputPath}");
                        return ExitCodes.Success;
                    }

                    DisplayError("Failed to remove columns");
                    return ExitCodes.Error;
                });
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }
}
