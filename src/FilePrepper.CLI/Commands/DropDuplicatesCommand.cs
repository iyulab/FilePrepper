using System.CommandLine;
using FilePrepper.Tasks;
using FilePrepper.Tasks.DropDuplicates;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

public class DropDuplicatesCommand : BaseCommand
{
    private readonly Option<string> _inputOption;
    private readonly Option<string> _outputOption;
    private readonly Option<bool> _keepFirstOption;
    private readonly Option<bool> _subsetOnlyOption;
    private readonly Option<string[]?> _columnsOption;

    public DropDuplicatesCommand(ILoggerFactory loggerFactory)
        : base("drop-duplicates", "Remove duplicate rows", loggerFactory)
    {
        _inputOption = new Option<string>(new[] { "--input", "-i" }, "Input file path") { IsRequired = true };
        _outputOption = new Option<string>(new[] { "--output", "-o" }, "Output file path") { IsRequired = true };
        _keepFirstOption = new Option<bool>("--keep-first", () => true, "Keep first occurrence of duplicates");
        _subsetOnlyOption = new Option<bool>("--subset-only", () => false, "Check duplicates only on specified columns");
        _columnsOption = new Option<string[]?>(
            aliases: new[] { "--columns", "-c" },
            description: "Columns to check for duplicates",
            parseArgument: result => result.Tokens.Count > 0 ? result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries) : null);

        AddOption(_inputOption);
        AddOption(_outputOption);
        AddOption(_keepFirstOption);
        AddOption(_subsetOnlyOption);
        AddOption(_columnsOption);

        this.SetHandler(async (context) =>
        {
            context.ExitCode = await ExecuteAsync(
                context.ParseResult.GetValueForOption(_inputOption)!,
                context.ParseResult.GetValueForOption(_outputOption)!,
                context.ParseResult.GetValueForOption(_keepFirstOption),
                context.ParseResult.GetValueForOption(_subsetOnlyOption),
                context.ParseResult.GetValueForOption(_columnsOption),
                context.ParseResult.GetValueForOption(CommonOptions.HasHeader),
                context.ParseResult.GetValueForOption(CommonOptions.IgnoreErrors),
                context.ParseResult.GetValueForOption(CommonOptions.Verbose));
        });
    }

    private async Task<int> ExecuteAsync(string inputPath, string outputPath, bool keepFirst, bool subsetOnly,
        string[]? columns, bool hasHeader, bool ignoreErrors, bool verbose)
    {
        try
        {
            if (subsetOnly && (columns == null || columns.Length == 0))
            {
                DisplayError("Columns required when using subset-only mode");
                return ExitCodes.InvalidArguments;
            }

            var options = new DropDuplicatesOption
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                KeepFirst = keepFirst,
                SubsetColumnsOnly = subsetOnly,
                TargetColumns = columns ?? Array.Empty<string>(),
                HasHeader = hasHeader,
                IgnoreErrors = ignoreErrors
            };

            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Dropping duplicates...", async ctx =>
                {
                    var taskLogger = LoggerFactory.CreateLogger<DropDuplicatesTask>();
                    var task = new DropDuplicatesTask(taskLogger);
                    var taskContext = new TaskContext(options);

                    var success = await task.ExecuteAsync(taskContext);
                    if (success)
                    {
                        DisplaySuccess($"Duplicates removed: {outputPath}");
                        return ExitCodes.Success;
                    }

                    DisplayError("Failed to drop duplicates");
                    return ExitCodes.Error;
                });
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }
}
