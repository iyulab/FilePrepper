using System.CommandLine;
using FilePrepper.Tasks;
using FilePrepper.Tasks.Merge;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

/// <summary>
/// Command to merge multiple files
/// </summary>
public class MergeCommand : BaseCommand
{
    private readonly Option<string[]> _inputFilesOption;
    private readonly Option<string> _outputOption;
    private readonly Option<string> _mergeTypeOption;
    private readonly Option<string> _joinTypeOption;
    private readonly Option<string[]> _keyColumnsOption;

    public MergeCommand(ILoggerFactory loggerFactory)
        : base("merge", "Merge multiple files vertically (concatenate) or horizontally (join)", loggerFactory)
    {
        // Required options
        _inputFilesOption = new Option<string[]>(
            aliases: new[] { "--input", "-i" },
            description: "Input file paths (space-separated)")
        { IsRequired = true, AllowMultipleArgumentsPerToken = true };

        _outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            description: "Output file path")
        { IsRequired = true };

        _mergeTypeOption = new Option<string>(
            aliases: new[] { "--type", "-t" },
            description: "Merge type: Vertical (concatenate rows) or Horizontal (join columns)")
        { IsRequired = true };

        // Optional options
        _joinTypeOption = new Option<string>(
            aliases: new[] { "--join-type", "-j" },
            getDefaultValue: () => "Inner",
            description: "Join type for horizontal merge: Inner, Left, Right, Full");

        _keyColumnsOption = new Option<string[]>(
            aliases: new[] { "--key-columns", "-k" },
            description: "Key columns for horizontal merge (comma-separated)",
            parseArgument: result =>
            {
                if (result.Tokens.Count == 0) return Array.Empty<string>();
                return result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries);
            });

        // Add all options
        AddOption(_inputFilesOption);
        AddOption(_outputOption);
        AddOption(_mergeTypeOption);
        AddOption(_joinTypeOption);
        AddOption(_keyColumnsOption);

        // Set the handler
        this.SetHandler(async (context) =>
        {
            var inputFiles = context.ParseResult.GetValueForOption(_inputFilesOption)!;
            var outputPath = context.ParseResult.GetValueForOption(_outputOption)!;
            var mergeType = context.ParseResult.GetValueForOption(_mergeTypeOption)!;
            var joinType = context.ParseResult.GetValueForOption(_joinTypeOption)!;
            var keyColumns = context.ParseResult.GetValueForOption(_keyColumnsOption) ?? Array.Empty<string>();
            var hasHeader = context.ParseResult.GetValueForOption(CommonOptions.HasHeader);
            var ignoreErrors = context.ParseResult.GetValueForOption(CommonOptions.IgnoreErrors);
            var verbose = context.ParseResult.GetValueForOption(CommonOptions.Verbose);

            context.ExitCode = await ExecuteAsync(
                inputFiles, outputPath, mergeType, joinType, keyColumns,
                hasHeader, ignoreErrors, verbose);
        });
    }

    private async Task<int> ExecuteAsync(
        string[] inputFiles, string outputPath, string mergeTypeStr, string joinTypeStr, string[] keyColumns,
        bool hasHeader, bool ignoreErrors, bool verbose)
    {
        try
        {
            // Parse and validate merge type
            if (!Enum.TryParse<MergeType>(mergeTypeStr, true, out var mergeType))
            {
                var validTypes = string.Join(", ", Enum.GetNames<MergeType>());
                DisplayError($"Invalid merge type '{mergeTypeStr}'. Valid values: {validTypes}");
                return ExitCodes.InvalidArguments;
            }

            // Parse and validate join type
            if (!Enum.TryParse<JoinType>(joinTypeStr, true, out var joinType))
            {
                var validTypes = string.Join(", ", Enum.GetNames<JoinType>());
                DisplayError($"Invalid join type '{joinTypeStr}'. Valid values: {validTypes}");
                return ExitCodes.InvalidArguments;
            }

            // Validation with rich output
            var validationResults = new List<(string Name, bool IsValid, string? Error)>
            {
                ("Input files count", inputFiles.Length >= 2, inputFiles.Length >= 2 ? null : "At least 2 files required"),
                ("Output directory", ValidateOutputPath(outputPath), ValidateOutputPath(outputPath) ? null : "Directory not found"),
                ("Merge type", true, $"{mergeType} merge"),
            };

            // Validate all input files
            foreach (var inputFile in inputFiles)
            {
                var isValid = ValidateInputFile(inputFile, out var error);
                validationResults.Add(($"Input: {Path.GetFileName(inputFile)}", isValid, error));
            }

            // Additional validation for horizontal merge
            if (mergeType == MergeType.Horizontal)
            {
                validationResults.Add(("Join type", true, $"{joinType} join"));

                if (keyColumns.Length > 0)
                {
                    validationResults.Add(("Key columns", true, $"{keyColumns.Length} column(s)"));
                }
                else
                {
                    validationResults.Add(("Key columns", false, "At least 1 key column required for horizontal merge"));
                }
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Parameter")
                .AddColumn("Status");

            foreach (var (name, isValid, error) in validationResults)
            {
                table.AddRow(
                    name,
                    isValid
                        ? "[green]✓ Valid[/]"
                        : $"[red]✗ {Markup.Escape(error ?? "Invalid")}[/]");
            }

            if (verbose)
            {
                AnsiConsole.Write(table);
            }

            if (validationResults.Any(r => !r.IsValid))
            {
                DisplayError("Validation failed. Please check your inputs.");
                return ExitCodes.InvalidArguments;
            }

            // Create options
            var options = new MergeOption
            {
                InputPaths = inputFiles.ToList(),
                OutputPath = outputPath,
                MergeType = mergeType,
                JoinType = joinType,
                JoinKeyColumns = keyColumns.Select(column =>
                {
                    if (int.TryParse(column, out int index))
                    {
                        return ColumnIdentifier.ByIndex(index);
                    }
                    return ColumnIdentifier.ByName(column);
                }).ToList(),
                HasHeader = hasHeader,
                IgnoreErrors = ignoreErrors
            };

            // Execute with progress display
            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Merging files...", async ctx =>
                {
                    ctx.Status($"Reading {inputFiles.Length} files...");

                    var taskLogger = LoggerFactory.CreateLogger<MergeTask>();
                    var task = new MergeTask(taskLogger);
                    var taskContext = new TaskContext(options);

                    Logger.LogInformation(
                        "Merging {Count} files using {MergeType} merge",
                        inputFiles.Length, mergeType);

                    if (verbose && mergeType == MergeType.Horizontal)
                    {
                        Logger.LogInformation("  Join type: {JoinType}", joinType);
                        Logger.LogInformation("  Key columns: {Keys}", string.Join(", ", keyColumns));
                    }

                    ctx.Status($"Applying {mergeType} merge...");
                    var success = await task.ExecuteAsync(taskContext);

                    if (success)
                    {
                        DisplaySuccess($"Files merged successfully: {outputPath}");

                        if (verbose)
                        {
                            var summaryText = mergeType == MergeType.Horizontal
                                ? $"""
                                    [bold]Summary:[/]
                                    • Input files: {inputFiles.Length}
                                    • Output: {Markup.Escape(outputPath)}
                                    • Merge type: {mergeType}
                                    • Join type: {joinType}
                                    • Key columns: {string.Join(", ", keyColumns)}
                                    • Has header: {hasHeader}
                                    """
                                : $"""
                                    [bold]Summary:[/]
                                    • Input files: {inputFiles.Length}
                                    • Output: {Markup.Escape(outputPath)}
                                    • Merge type: {mergeType} (concatenate)
                                    • Has header: {hasHeader}
                                    """;

                            var panel = new Panel(new Markup(summaryText))
                            {
                                Header = new PanelHeader("Merge Complete", Justify.Center),
                                BorderStyle = new Style(Color.Green)
                            };
                            AnsiConsole.Write(panel);
                        }

                        return ExitCodes.Success;
                    }

                    DisplayError("Failed to merge files");
                    return ExitCodes.Error;
                });
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    private bool ValidateOutputPath(string outputPath)
    {
        var outputDir = Path.GetDirectoryName(outputPath);
        return string.IsNullOrEmpty(outputDir) || Directory.Exists(outputDir);
    }
}
