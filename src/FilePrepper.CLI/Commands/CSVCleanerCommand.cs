using System.CommandLine;
using FilePrepper.Tasks;
using FilePrepper.Tasks.CSVCleaner;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

/// <summary>
/// Command to clean CSV numeric data by removing thousand separators
/// </summary>
public class CSVCleanerCommand : BaseCommand
{
    private readonly Option<string> _inputOption;
    private readonly Option<string> _outputOption;
    private readonly Option<string[]> _targetColumnsOption;
    private readonly Option<char> _separatorOption;
    private readonly Option<bool> _validateOption;

    public CSVCleanerCommand(ILoggerFactory loggerFactory)
        : base("clean", "Clean CSV numeric data (remove thousand separators)", loggerFactory)
    {
        // Required options
        _inputOption = new Option<string>("--input", new[] { "-i" }) { Description = "Input file path", Required = true };

        _outputOption = new Option<string>("--output", new[] { "-o" }) { Description = "Output file path", Required = true };

        // Optional options
        _targetColumnsOption = new Option<string[]>("--columns", new[] { "-c" }) { Description = "Target columns to clean (if empty, all columns are cleaned, space-separated)", DefaultValueFactory = _ => Array.Empty<string>(), AllowMultipleArgumentsPerToken = true };

        _separatorOption = new Option<char>("--separator", new[] { "-s" }) { Description = "Thousand separator character to remove", DefaultValueFactory = _ => ',' };

        _validateOption = new Option<bool>("--validate", new[] { "-val" }) { Description = "Validate that cleaned values are valid numbers", DefaultValueFactory = _ => false };

        // Add all options
        Add(_inputOption);
        Add(_outputOption);
        Add(_targetColumnsOption);
        Add(_separatorOption);
        Add(_validateOption);

        // Set the handler
        this.SetAction(async (parseResult) =>
        {
            var inputPath = parseResult.GetValue(_inputOption)!;
            var outputPath = parseResult.GetValue(_outputOption)!;
            var targetColumns = parseResult.GetValue(_targetColumnsOption) ?? Array.Empty<string>();
            var separator = parseResult.GetValue(_separatorOption);
            var validate = parseResult.GetValue(_validateOption);
            var hasHeader = parseResult.GetValue(CommonOptions.HasHeader);
            var ignoreErrors = parseResult.GetValue(CommonOptions.IgnoreErrors);
            var verbose = parseResult.GetValue(CommonOptions.Verbose);

            return await ExecuteAsync(
                inputPath, outputPath, targetColumns, separator, validate,
                hasHeader, ignoreErrors, verbose);
        });
    }

    private async Task<int> ExecuteAsync(
        string inputPath, string outputPath, string[] targetColumns, char separator, bool validate,
        bool hasHeader, bool ignoreErrors, bool verbose)
    {
        try
        {
            // Validation with rich output
            var validationResults = new List<(string Name, bool IsValid, string? Error)>
            {
                ("Input file", ValidateInputFile(inputPath, out var inputError), inputError),
                ("Output directory", ValidateOutputPath(outputPath), ValidateOutputPath(outputPath) ? null : "Directory not found"),
                ("Target columns", true, targetColumns.Length > 0 ? $"{targetColumns.Length} column(s)" : "All columns"),
                ("Thousand separator", true, $"'{separator}'"),
                ("Validate numeric", true, validate ? "Enabled" : "Disabled"),
            };

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
            var options = new CSVCleanerOption
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                TargetColumns = targetColumns.ToList(),
                ThousandSeparator = separator,
                RemoveWhitespace = true,
                ValidateNumeric = validate,
                HasHeader = hasHeader,
                IgnoreErrors = ignoreErrors
            };

            // Execute with progress display
            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Cleaning CSV data...", async ctx =>
                {
                    ctx.Status("Reading input file...");

                    var taskLogger = LoggerFactory.CreateLogger<CSVCleanerTask>();
                    var task = new CSVCleanerTask(taskLogger);
                    var taskContext = new TaskContext(options);

                    Logger.LogInformation("Cleaning CSV data from {Input}", Path.GetFileName(inputPath));

                    if (verbose)
                    {
                        Logger.LogInformation("  Target columns: {Columns}",
                            targetColumns.Length > 0 ? string.Join(", ", targetColumns) : "All columns");
                        Logger.LogInformation("  Thousand separator: '{Separator}'", separator);
                    }

                    ctx.Status("Removing thousand separators...");
                    var success = await task.ExecuteAsync(taskContext);

                    if (success)
                    {
                        DisplaySuccess($"CSV cleaned successfully: {outputPath}");

                        if (verbose)
                        {
                            var summaryText = $"""
                                [bold]Summary:[/]
                                • Input: {Markup.Escape(inputPath)}
                                • Output: {Markup.Escape(outputPath)}
                                • Target columns: {(targetColumns.Length > 0 ? targetColumns.Length : "All")}
                                • Thousand separator: '{separator}'
                                • Validate numeric: {validate}
                                • Has header: {hasHeader}
                                """;

                            var panel = new Panel(new Markup(summaryText))
                            {
                                Header = new PanelHeader("CSV Cleaning Complete", Justify.Center),
                                BorderStyle = new Style(Color.Green)
                            };
                            AnsiConsole.Write(panel);
                        }

                        return ExitCodes.Success;
                    }

                    DisplayError("Failed to clean CSV data");
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
