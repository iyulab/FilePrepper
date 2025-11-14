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
        _inputOption = new Option<string>("--input", new[] { "-i" }) { Description = "Input file path", Required = true };
        _outputOption = new Option<string>("--output", new[] { "-o" }) { Description = "Output file path", Required = true };
        _keepFirstOption = new Option<bool>("--keep-first") { Description = "Keep first occurrence of duplicates", DefaultValueFactory = _ => true };
        _subsetOnlyOption = new Option<bool>("--subset-only") { Description = "Check duplicates only on specified columns", DefaultValueFactory = _ => false };
        _columnsOption = new Option<string[]?>("--columns", new[] { "-c" })
        {
            Description = "Columns to check for duplicates",
            CustomParser = result => result.Tokens.Count > 0 ? result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries) : null
        };

        Add(_inputOption);
        Add(_outputOption);
        Add(_keepFirstOption);
        Add(_subsetOnlyOption);
        Add(_columnsOption);

        this.SetAction(async (parseResult) =>
        {
            return await ExecuteAsync(
                parseResult.GetValue(_inputOption)!,
                parseResult.GetValue(_outputOption)!,
                parseResult.GetValue(_keepFirstOption),
                parseResult.GetValue(_subsetOnlyOption),
                parseResult.GetValue(_columnsOption),
                parseResult.GetValue(CommonOptions.HasHeader),
                parseResult.GetValue(CommonOptions.IgnoreErrors),
                parseResult.GetValue(CommonOptions.Verbose));
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
