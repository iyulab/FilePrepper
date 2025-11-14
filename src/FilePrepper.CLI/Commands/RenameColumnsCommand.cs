using System.CommandLine;
using FilePrepper.Tasks;
using FilePrepper.Tasks.RenameColumns;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

public class RenameColumnsCommand : BaseCommand
{
    private readonly Option<string> _inputOption;
    private readonly Option<string> _outputOption;
    private readonly Option<string[]> _mappingsOption;

    public RenameColumnsCommand(ILoggerFactory loggerFactory)
        : base("rename-columns", "Rename columns in the input file", loggerFactory)
    {
        _inputOption = new Option<string>("--input", new[] { "-i" }) { Description = "Input file path", Required = true };
        _outputOption = new Option<string>("--output", new[] { "-o" }) { Description = "Output file path", Required = true };
        _mappingsOption = new Option<string[]>("--mappings", new[] { "-m" }) { Description = "Column rename mappings in format oldName:newName", CustomParser = result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries), Required = true };

        Add(_inputOption);
        Add(_outputOption);
        Add(_mappingsOption);

        this.SetAction(async (parseResult) =>
        {
            return await ExecuteAsync(
                parseResult.GetValue(_inputOption)!,
                parseResult.GetValue(_outputOption)!,
                parseResult.GetValue(_mappingsOption)!,
                parseResult.GetValue(CommonOptions.HasHeader),
                parseResult.GetValue(CommonOptions.IgnoreErrors),
                parseResult.GetValue(CommonOptions.Verbose));
        });
    }

    private async Task<int> ExecuteAsync(string inputPath, string outputPath, string[] mappingStrings,
        bool hasHeader, bool ignoreErrors, bool verbose)
    {
        try
        {
            var renameMap = new Dictionary<string, string>();

            foreach (var mapping in mappingStrings)
            {
                var parts = mapping.Split(':');
                if (parts.Length != 2)
                {
                    DisplayError($"Invalid mapping format: {mapping}");
                    return ExitCodes.InvalidArguments;
                }

                var oldName = parts[0].Trim();
                var newName = parts[1].Trim();

                if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
                {
                    DisplayError($"Column names cannot be empty: {mapping}");
                    return ExitCodes.InvalidArguments;
                }

                if (renameMap.ContainsKey(oldName))
                {
                    DisplayError($"Duplicate source column: {oldName}");
                    return ExitCodes.InvalidArguments;
                }

                renameMap[oldName] = newName;
            }

            var options = new RenameColumnsOption
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                RenameMap = renameMap,
                HasHeader = hasHeader,
                IgnoreErrors = ignoreErrors
            };

            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Renaming columns...", async ctx =>
                {
                    var taskLogger = LoggerFactory.CreateLogger<RenameColumnsTask>();
                    var task = new RenameColumnsTask(taskLogger);
                    var taskContext = new TaskContext(options);

                    var success = await task.ExecuteAsync(taskContext);
                    if (success)
                    {
                        DisplaySuccess($"Columns renamed: {outputPath}");
                        return ExitCodes.Success;
                    }

                    DisplayError("Failed to rename columns");
                    return ExitCodes.Error;
                });
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }
}
