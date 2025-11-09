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
        _inputOption = new Option<string>(
            aliases: new[] { "--input", "-i" },
            description: "Input file path")
        { IsRequired = true };

        _outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            description: "Output file path")
        { IsRequired = true };

        _columnOption = new Option<string>(
            aliases: new[] { "--column", "-c" },
            description: "Column to parse/transform")
        { IsRequired = true };

        // Mode option
        _modeOption = new Option<string>(
            aliases: new[] { "--mode", "-m" },
            getDefaultValue: () => "parse",
            description: "Operation mode: parse, excel, features");

        // Format options
        _inputFormatOption = new Option<string?>(
            aliases: new[] { "--format", "-f" },
            description: "Input format for parse mode (e.g., 'yyyy-MM-dd HH:mm', 'yyyyMMddHHmm')");

        _outputFormatOption = new Option<string>(
            aliases: new[] { "--output-format", "-of" },
            getDefaultValue: () => "yyyy-MM-dd HH:mm:ss",
            description: "Output format");

        // Feature extraction options
        _featuresOption = new Option<string?>(
            aliases: new[] { "--features", "-ft" },
            description: "Features to extract (comma-separated): Year,Month,Day,Hour,Minute,DayOfWeek,DayOfYear,WeekOfYear,Quarter");

        _removeOriginalOption = new Option<bool>(
            aliases: new[] { "--remove-original" },
            getDefaultValue: () => false,
            description: "Remove original column after feature extraction");

        // Add all options
        AddOption(_inputOption);
        AddOption(_outputOption);
        AddOption(_columnOption);
        AddOption(_modeOption);
        AddOption(_inputFormatOption);
        AddOption(_outputFormatOption);
        AddOption(_featuresOption);
        AddOption(_removeOriginalOption);

        // Set the handler
        this.SetHandler(async (context) =>
        {
            var inputPath = context.ParseResult.GetValueForOption(_inputOption)!;
            var outputPath = context.ParseResult.GetValueForOption(_outputOption)!;
            var column = context.ParseResult.GetValueForOption(_columnOption)!;
            var mode = context.ParseResult.GetValueForOption(_modeOption)!;
            var inputFormat = context.ParseResult.GetValueForOption(_inputFormatOption);
            var outputFormat = context.ParseResult.GetValueForOption(_outputFormatOption)!;
            var features = context.ParseResult.GetValueForOption(_featuresOption);
            var removeOriginal = context.ParseResult.GetValueForOption(_removeOriginalOption);
            var hasHeader = context.ParseResult.GetValueForOption(CommonOptions.HasHeader);
            var ignoreErrors = context.ParseResult.GetValueForOption(CommonOptions.IgnoreErrors);
            var verbose = context.ParseResult.GetValueForOption(CommonOptions.Verbose);

            context.ExitCode = await ExecuteAsync(
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
