using System.CommandLine;
using FilePrepper.Tasks;
using FilePrepper.Tasks.ReorderColumns;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

public class ReorderColumnsCommand : BaseCommand
{
    private readonly Option<string> _inputOption;
    private readonly Option<string> _outputOption;
    private readonly Option<string[]> _orderOption;

    public ReorderColumnsCommand(ILoggerFactory loggerFactory)
        : base("reorder-columns", "Reorder columns in the specified order", loggerFactory)
    {
        _inputOption = new Option<string>("--input", new[] { "-i" }) { Description = "Input file path", Required = true };
        _outputOption = new Option<string>("--output", new[] { "-o" }) { Description = "Output file path", Required = true };
        _orderOption = new Option<string[]>("--order") { Description = "Desired column order", CustomParser = result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries), Required = true };

        Add(_inputOption);
        Add(_outputOption);
        Add(_orderOption);

        this.SetAction(async (parseResult) =>
        {
            return await ExecuteAsync(
                parseResult.GetValue(_inputOption)!,
                parseResult.GetValue(_outputOption)!,
                parseResult.GetValue(_orderOption)!,
                parseResult.GetValue(CommonOptions.HasHeader),
                parseResult.GetValue(CommonOptions.IgnoreErrors),
                parseResult.GetValue(CommonOptions.Verbose));
        });
    }

    private async Task<int> ExecuteAsync(string inputPath, string outputPath, string[] order,
        bool hasHeader, bool ignoreErrors, bool verbose)
    {
        try
        {
            var options = new ReorderColumnsOption
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                Order = order.ToList(),
                HasHeader = hasHeader,
                IgnoreErrors = ignoreErrors
            };

            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Reordering columns...", async ctx =>
                {
                    var taskLogger = LoggerFactory.CreateLogger<ReorderColumnsTask>();
                    var task = new ReorderColumnsTask(taskLogger);
                    var taskContext = new TaskContext(options);

                    var success = await task.ExecuteAsync(taskContext);
                    if (success)
                    {
                        DisplaySuccess($"Columns reordered: {outputPath}");
                        return ExitCodes.Success;
                    }

                    DisplayError("Failed to reorder columns");
                    return ExitCodes.Error;
                });
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }
}
