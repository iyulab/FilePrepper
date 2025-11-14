using System.CommandLine;
using FilePrepper.Tasks;
using FilePrepper.Tasks.DateTimeOps;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

/// <summary>
/// Command for DateTime parsing and feature extraction
/// </summary>
public class DateTimeCommand : BaseCommand
{
    private readonly Option<string> _inputOption;
    private readonly Option<string> _outputOption;
    private readonly Option<string> _columnOption;
    private readonly Option<string> _modeOption;
    private readonly Option<string?> _inputFormatOption;
    private readonly Option<string> _outputFormatOption;
    private readonly Option<string?> _featuresOption;
    private readonly Option<bool> _removeOriginalOption;

    public DateTimeCommand(ILoggerFactory loggerFactory)
        : base("datetime", "Parse DateTime and extract features", loggerFactory)
    {
        // Required options
        _inputOption = new Option<string>("--input", new[] { "-i" }) { Description = "Input file path", Required = true };

        _outputOption = new Option<string>("--output", new[] { "-o" }) { Description = "Output file path", Required = true };

        _columnOption = new Option<string>("--column", new[] { "-c" }) { Description = "Column to parse/transform", Required = true };

        // Mode option
        _modeOption = new Option<string>("--mode", new[] { "-m" }) { Description = "Operation mode: parse, excel, features", DefaultValueFactory = _ => "parse" };

        // Format options
        _inputFormatOption = new Option<string?>("--format", new[] { "-f" }) { Description = "Input format for parse mode (e.g., 'yyyy-MM-dd HH:mm', 'yyyyMMddHHmm')" };

        _outputFormatOption = new Option<string>("--output-format", new[] { "-of" }) { Description = "Output format", DefaultValueFactory = _ => "yyyy-MM-dd HH:mm:ss" };

        // Feature extraction options
        _featuresOption = new Option<string?>("--features", new[] { "-ft" }) { Description = "Features to extract (comma-separated): Year,Month,Day,Hour,Minute,DayOfWeek,DayOfYear,WeekOfYear,Quarter" };

        _removeOriginalOption = new Option<bool>("--remove-original") { Description = "Remove original column after feature extraction", DefaultValueFactory = _ => false };

        // Add all options
        Add(_inputOption);
        Add(_outputOption);
        Add(_columnOption);
        Add(_modeOption);
        Add(_inputFormatOption);
        Add(_outputFormatOption);
        Add(_featuresOption);
        Add(_removeOriginalOption);

        // Set the handler
        this.SetAction(async (parseResult) =>
        {
            var inputPath = parseResult.GetValue(_inputOption)!;
            var outputPath = parseResult.GetValue(_outputOption)!;
            var column = parseResult.GetValue(_columnOption)!;
            var mode = parseResult.GetValue(_modeOption)!;
            var inputFormat = parseResult.GetValue(_inputFormatOption);
            var outputFormat = parseResult.GetValue(_outputFormatOption)!;
            var features = parseResult.GetValue(_featuresOption);
            var removeOriginal = parseResult.GetValue(_removeOriginalOption);
            var hasHeader = parseResult.GetValue(CommonOptions.HasHeader);
            var ignoreErrors = parseResult.GetValue(CommonOptions.IgnoreErrors);
            var verbose = parseResult.GetValue(CommonOptions.Verbose);

            return await ExecuteAsync(
                inputPath, outputPath, column, mode, inputFormat, outputFormat, features, removeOriginal,
                hasHeader, ignoreErrors, verbose);
        });
    }

    private async Task<int> ExecuteAsync(
        string inputPath, string outputPath, string column, string mode, string? inputFormat, string outputFormat,
        string? features, bool removeOriginal, bool hasHeader, bool ignoreErrors, bool verbose)
    {
        try
        {
            // Parse mode
            var dateTimeMode = mode.ToLower() switch
            {
                "parse" => DateTimeMode.Parse,
                "excel" => DateTimeMode.ParseExcel,
                "features" => DateTimeMode.ExtractFeatures,
                _ => throw new ArgumentException($"Invalid mode: {mode}. Valid modes: parse, excel, features")
            };

            // Validation with rich output
            var validationResults = new List<(string Name, bool IsValid, string? Error)>
            {
                ("Input file", ValidateInputFile(inputPath, out var inputError), inputError),
                ("Output directory", ValidateOutputPath(outputPath), ValidateOutputPath(outputPath) ? null : "Directory not found"),
                ("Column", !string.IsNullOrWhiteSpace(column), column),
                ("Mode", true, mode),
            };

            if (dateTimeMode == DateTimeMode.Parse)
            {
                validationResults.Add(("Input format", !string.IsNullOrWhiteSpace(inputFormat), inputFormat ?? "Required for parse mode"));
            }

            if (dateTimeMode == DateTimeMode.ExtractFeatures)
            {
                validationResults.Add(("Features", !string.IsNullOrWhiteSpace(features), features ?? "Required for features mode"));
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
            var options = new DateTimeOption
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                Column = column,
                Mode = dateTimeMode,
                InputFormat = inputFormat,
                OutputFormat = outputFormat,
                Features = features,
                RemoveOriginal = removeOriginal,
                HasHeader = hasHeader,
                IgnoreErrors = ignoreErrors
            };

            // Execute with progress display
            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Processing DateTime...", async ctx =>
                {
                    ctx.Status("Reading input file...");

                    var taskLogger = LoggerFactory.CreateLogger<DateTimeTask>();
                    var task = new DateTimeTask(taskLogger);
                    var taskContext = new TaskContext(options);

                    Logger.LogInformation("Processing DateTime in column: {Column}", column);

                    if (verbose)
                    {
                        Logger.LogInformation("  Mode: {Mode}", mode);
                        if (dateTimeMode == DateTimeMode.Parse)
                        {
                            Logger.LogInformation("  Input format: {InputFormat}", inputFormat);
                        }
                        Logger.LogInformation("  Output format: {OutputFormat}", outputFormat);
                    }

                    ctx.Status($"Applying {mode} operation...");
                    var success = await task.ExecuteAsync(taskContext);

                    if (success)
                    {
                        DisplaySuccess($"DateTime processed successfully: {outputPath}");

                        if (verbose)
                        {
                            var summaryText = $"""
                                [bold]Summary:[/]
                                • Input: {Markup.Escape(inputPath)}
                                • Output: {Markup.Escape(outputPath)}
                                • Column: {column}
                                • Mode: {mode}
                                • Output format: {outputFormat}
                                • Has header: {hasHeader}
                                """;

                            var panel = new Panel(new Markup(summaryText))
                            {
                                Header = new PanelHeader("DateTime Processing Complete", Justify.Center),
                                BorderStyle = new Style(Color.Green)
                            };
                            AnsiConsole.Write(panel);
                        }

                        return ExitCodes.Success;
                    }

                    DisplayError("Failed to process DateTime");
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
