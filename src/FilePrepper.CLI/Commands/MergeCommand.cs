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
    private readonly Option<string[]> _joinMappingsOption;

    public MergeCommand(ILoggerFactory loggerFactory)
        : base("merge", "Merge multiple files vertically (concatenate) or horizontally (join)", loggerFactory)
    {
        // Required options
        _inputFilesOption = new Option<string[]>("--input", new[] { "-i" }) { Description = "Input file paths (space-separated)", Required = true, AllowMultipleArgumentsPerToken = true };

        _outputOption = new Option<string>("--output", new[] { "-o" }) { Description = "Output file path", Required = true };

        _mergeTypeOption = new Option<string>("--type", new[] { "-t" }) { Description = "Merge type: Vertical (concatenate rows) or Horizontal (join columns)", Required = true };

        // Optional options
        _joinTypeOption = new Option<string>("--join-type", new[] { "-j" }) { Description = "Join type for horizontal merge: Inner, Left, Right, Full", DefaultValueFactory = _ => "Inner" };

        _keyColumnsOption = new Option<string[]>("--key-columns", new[] { "-k" })
        {
            Description = "Key columns for horizontal merge (comma-separated)",
            CustomParser = result =>
            {
                if (result.Tokens.Count == 0) return Array.Empty<string>();
                return result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries);
            }
        };

        _joinMappingsOption = new Option<string[]>("--join-mappings", new[] { "-m" }) { Description = "Join column mappings for heterogeneous column names (format: 'left:right' or 'left:right:output', space-separated)", AllowMultipleArgumentsPerToken = true };

        // Add all options
        Add(_inputFilesOption);
        Add(_outputOption);
        Add(_mergeTypeOption);
        Add(_joinTypeOption);
        Add(_keyColumnsOption);
        Add(_joinMappingsOption);

        // Set the handler
        this.SetAction(async (parseResult) =>
        {
            var inputFiles = parseResult.GetValue(_inputFilesOption)!;
            var outputPath = parseResult.GetValue(_outputOption)!;
            var mergeType = parseResult.GetValue(_mergeTypeOption)!;
            var joinType = parseResult.GetValue(_joinTypeOption)!;
            var keyColumns = parseResult.GetValue(_keyColumnsOption) ?? Array.Empty<string>();
            var joinMappings = parseResult.GetValue(_joinMappingsOption) ?? Array.Empty<string>();
            var hasHeader = parseResult.GetValue(CommonOptions.HasHeader);
            var ignoreErrors = parseResult.GetValue(CommonOptions.IgnoreErrors);
            var verbose = parseResult.GetValue(CommonOptions.Verbose);

            return await ExecuteAsync(
                inputFiles, outputPath, mergeType, joinType, keyColumns, joinMappings,
                hasHeader, ignoreErrors, verbose);
        });
    }

    private async Task<int> ExecuteAsync(
        string[] inputFiles, string outputPath, string mergeTypeStr, string joinTypeStr,
        string[] keyColumns, string[] joinMappings,
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

                bool hasJoinConfig = keyColumns.Length > 0 || joinMappings.Length > 0;

                if (keyColumns.Length > 0)
                {
                    validationResults.Add(("Key columns", true, $"{keyColumns.Length} column(s)"));
                }
                else if (joinMappings.Length > 0)
                {
                    validationResults.Add(("Join mappings", true, $"{joinMappings.Length} mapping(s)"));
                }
                else
                {
                    validationResults.Add(("Join configuration", false, "Either key columns or join mappings required for horizontal merge"));
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
                JoinMappings = joinMappings.Select(JoinMapping.Parse).ToList(),
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

                        if (keyColumns.Length > 0)
                        {
                            Logger.LogInformation("  Key columns: {Keys}", string.Join(", ", keyColumns));
                        }

                        if (joinMappings.Length > 0)
                        {
                            Logger.LogInformation("  Join mappings: {Mappings}", string.Join(", ", joinMappings));
                        }
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
