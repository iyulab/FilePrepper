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
        _inputOption = new Option<string>("--input", new[] { "-i" }) { Description = "Input file path", Required = true };
        _outputOption = new Option<string>("--output", new[] { "-o" }) { Description = "Output file path", Required = true };
        _columnsOption = new Option<string[]>("--columns", new[] { "-c" }) { Description = "Columns to remove", CustomParser = result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries), Required = true };

        Add(_inputOption);
        Add(_outputOption);
        Add(_columnsOption);

        this.SetAction(async (parseResult) =>
        {
            return await ExecuteAsync(
                parseResult.GetValue(_inputOption)!,
                parseResult.GetValue(_outputOption)!,
                parseResult.GetValue(_columnsOption)!,
                parseResult.GetValue(CommonOptions.HasHeader),
                parseResult.GetValue(CommonOptions.IgnoreErrors),
                parseResult.GetValue(CommonOptions.Verbose));
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
