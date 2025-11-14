using System.CommandLine;
using FilePrepper.Tasks;
using FilePrepper.Tasks.MergeAsOf;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

/// <summary>
/// Command for time-series merge (merge_asof) operations
/// </summary>
public class MergeAsOfCommand : BaseCommand
{
    private readonly Option<string[]> _inputFilesOption;
    private readonly Option<string> _outputOption;
    private readonly Option<string> _leftOnOption;
    private readonly Option<string> _rightOnOption;
    private readonly Option<string> _directionOption;
    private readonly Option<double?> _toleranceOption;
    private readonly Option<string> _suffixOption;

    public MergeAsOfCommand(ILoggerFactory loggerFactory)
        : base("merge-asof", "Merge two files based on nearest time matching (merge_asof)", loggerFactory)
    {
        // Required options
        _inputFilesOption = new Option<string[]>("--input", new[] { "-i" }) { Description = "Exactly 2 input file paths (left and right files, space-separated)", Required = true, AllowMultipleArgumentsPerToken = true };

        _outputOption = new Option<string>("--output", new[] { "-o" }) { Description = "Output file path", Required = true };

        _leftOnOption = new Option<string>("--left-on", new[] { "-l" }) { Description = "Column name in left file to use for matching (typically datetime)", Required = true };

        _rightOnOption = new Option<string>("--right-on", new[] { "-r" }) { Description = "Column name in right file to use for matching (typically datetime)", Required = true };

        // Optional options
        _directionOption = new Option<string>("--direction", new[] { "-d" }) { Description = "Matching direction: Backward (most recent <=), Forward (nearest >=), or Nearest (closest)", DefaultValueFactory = _ => "Backward" };

        _toleranceOption = new Option<double?>("--tolerance", new[] { "-t" }) { Description = "Maximum time difference allowed for matching (in seconds). If not specified, no tolerance limit is applied." };

        _suffixOption = new Option<string>("--suffix", new[] { "-s" }) { Description = "Suffix to add to right file columns to avoid name conflicts", DefaultValueFactory = _ => "_right" };

        // Add all options
        Add(_inputFilesOption);
        Add(_outputOption);
        Add(_leftOnOption);
        Add(_rightOnOption);
        Add(_directionOption);
        Add(_toleranceOption);
        Add(_suffixOption);

        // Set the handler
        this.SetAction(async (parseResult) =>
        {
            var inputFiles = parseResult.GetValue(_inputFilesOption)!;
            var outputPath = parseResult.GetValue(_outputOption)!;
            var leftOn = parseResult.GetValue(_leftOnOption)!;
            var rightOn = parseResult.GetValue(_rightOnOption)!;
            var directionStr = parseResult.GetValue(_directionOption)!;
            var tolerance = parseResult.GetValue(_toleranceOption);
            var suffix = parseResult.GetValue(_suffixOption)!;
            var hasHeader = parseResult.GetValue(CommonOptions.HasHeader);
            var ignoreErrors = parseResult.GetValue(CommonOptions.IgnoreErrors);
            var verbose = parseResult.GetValue(CommonOptions.Verbose);

            return await ExecuteAsync(
                inputFiles, outputPath, leftOn, rightOn, directionStr, tolerance, suffix,
                hasHeader, ignoreErrors, verbose);
        });
    }

    private async Task<int> ExecuteAsync(
        string[] inputFiles, string outputPath, string leftOn, string rightOn,
        string directionStr, double? tolerance, string suffix,
        bool hasHeader, bool ignoreErrors, bool verbose)
    {
        try
        {
            // Parse and validate direction
            if (!Enum.TryParse<AsOfDirection>(directionStr, true, out var direction))
            {
                var validDirections = string.Join(", ", Enum.GetNames<AsOfDirection>());
                DisplayError($"Invalid direction '{directionStr}'. Valid values: {validDirections}");
                return ExitCodes.InvalidArguments;
            }

            // Validation with rich output
            var validationResults = new List<(string Name, bool IsValid, string? Error)>
            {
                ("Input files count", inputFiles.Length == 2, inputFiles.Length == 2 ? "2 files (left & right)" : $"Expected 2 files, got {inputFiles.Length}"),
                ("Output directory", ValidateOutputPath(outputPath), ValidateOutputPath(outputPath) ? null : "Directory not found"),
                ("Header required", hasHeader, hasHeader ? "Header enabled" : "Header is required for merge_asof"),
                ("Direction", true, $"{direction}"),
            };

            // Validate input files
            foreach (var (inputFile, index) in inputFiles.Select((f, i) => (f, i)))
            {
                var isValid = ValidateInputFile(inputFile, out var error);
                var label = index == 0 ? "Left file" : "Right file";
                validationResults.Add(($"{label}: {Path.GetFileName(inputFile)}", isValid, error));
            }

            // Tolerance validation
            if (tolerance.HasValue)
            {
                validationResults.Add((
                    "Tolerance",
                    tolerance.Value >= 0,
                    tolerance.Value >= 0 ? $"{tolerance.Value} seconds" : "Must be non-negative"
                ));
            }
            else
            {
                validationResults.Add(("Tolerance", true, "No limit"));
            }

            // Display validation table
            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Parameter")
                .AddColumn("Status");

            foreach (var (name, isValid, error) in validationResults)
            {
                table.AddRow(
                    name,
                    isValid
                        ? $"[green]✓ {Markup.Escape(error ?? "Valid")}[/]"
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
            var options = new MergeAsOfOption
            {
                InputPaths = inputFiles.ToList(),
                OutputPath = outputPath,
                LeftOnColumn = leftOn,
                RightOnColumn = rightOn,
                Direction = direction,
                Tolerance = tolerance,
                Suffix = suffix,
                HasHeader = hasHeader,
                IgnoreErrors = ignoreErrors
            };

            // Execute with progress display
            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Performing merge_asof...", async ctx =>
                {
                    ctx.Status("Reading files...");

                    var taskLogger = LoggerFactory.CreateLogger<MergeAsOfTask>();
                    var task = new MergeAsOfTask(taskLogger);
                    var taskContext = new TaskContext(options);

                    Logger.LogInformation("Processing merge_asof");
                    Logger.LogInformation("  Left file: {Left}", inputFiles[0]);
                    Logger.LogInformation("  Right file: {Right}", inputFiles[1]);
                    Logger.LogInformation("  Left column: {LeftCol}", leftOn);
                    Logger.LogInformation("  Right column: {RightCol}", rightOn);
                    Logger.LogInformation("  Direction: {Direction}", direction);

                    if (tolerance.HasValue)
                    {
                        Logger.LogInformation("  Tolerance: {Tolerance} seconds", tolerance.Value);
                    }

                    ctx.Status($"Applying {direction} merge_asof...");
                    var success = await task.ExecuteAsync(taskContext);

                    if (success)
                    {
                        DisplaySuccess($"merge_asof completed successfully: {outputPath}");

                        if (verbose)
                        {
                            var summaryText = $"""
                                [bold]Summary:[/]
                                • Left file: {Markup.Escape(Path.GetFileName(inputFiles[0]))}
                                • Right file: {Markup.Escape(Path.GetFileName(inputFiles[1]))}
                                • Output: {Markup.Escape(outputPath)}
                                • Direction: {direction}
                                • Left column: {leftOn}
                                • Right column: {rightOn}
                                • Tolerance: {(tolerance.HasValue ? $"{tolerance.Value} seconds" : "No limit")}
                                • Suffix: {suffix}
                                """;

                            var panel = new Panel(new Markup(summaryText))
                            {
                                Header = new PanelHeader("merge_asof Complete", Justify.Center),
                                BorderStyle = new Style(Color.Green)
                            };
                            AnsiConsole.Write(panel);
                        }

                        return ExitCodes.Success;
                    }

                    DisplayError("Failed to perform merge_asof");
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
