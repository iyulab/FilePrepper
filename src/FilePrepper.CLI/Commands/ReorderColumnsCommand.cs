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
        _inputOption = new Option<string>(new[] { "--input", "-i" }, "Input file path") { IsRequired = true };
        _outputOption = new Option<string>(new[] { "--output", "-o" }, "Output file path") { IsRequired = true };
        _orderOption = new Option<string[]>(
            aliases: new[] { "--order" },
            description: "Desired column order",
            parseArgument: result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries)) { IsRequired = true };

        AddOption(_inputOption);
        AddOption(_outputOption);
        AddOption(_orderOption);

        this.SetHandler(async (context) =>
        {
            context.ExitCode = await ExecuteAsync(
                context.ParseResult.GetValueForOption(_inputOption)!,
                context.ParseResult.GetValueForOption(_outputOption)!,
                context.ParseResult.GetValueForOption(_orderOption)!,
                context.ParseResult.GetValueForOption(CommonOptions.HasHeader),
                context.ParseResult.GetValueForOption(CommonOptions.IgnoreErrors),
                context.ParseResult.GetValueForOption(CommonOptions.Verbose));
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
