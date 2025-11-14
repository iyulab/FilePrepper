using System.CommandLine;
using FilePrepper.Tasks;
using FilePrepper.Tasks.Conditional;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

/// <summary>
/// Command for creating conditional columns based on if-then-else logic
/// </summary>
public class ConditionalCommand : BaseCommand
{
    private readonly Option<string> _inputOption;
    private readonly Option<string> _outputOption;
    private readonly Option<string> _outputColumnOption;
    private readonly Option<string[]> _conditionsOption;
    private readonly Option<string> _elseValueOption;

    public ConditionalCommand(ILoggerFactory loggerFactory)
        : base("conditional", "Create a new column based on conditional logic (if-then-else)", loggerFactory)
    {
        // Required options
        _inputOption = new Option<string>("--input", new[] { "-i" }) { Description = "Input file path", Required = true };

        _outputOption = new Option<string>("--output", new[] { "-o" }) { Description = "Output file path", Required = true };

        _outputColumnOption = new Option<string>("--output-column", new[] { "-oc" }) { Description = "Name of the new column to create", Required = true };

        _conditionsOption = new Option<string[]>("--conditions", new[] { "-c" }) { Description = "Condition-value pairs (format: 'Column operator Value : ResultValue'). Example: 'Price > 100 : High'. Multiple conditions evaluated in order (first match wins).", Required = true, AllowMultipleArgumentsPerToken = true };

        // Optional options
        _elseValueOption = new Option<string>("--else", new[] { "-e" }) { Description = "Default value if no conditions match (else value)", DefaultValueFactory = _ => string.Empty };

        // Add all options
        Add(_inputOption);
        Add(_outputOption);
        Add(_outputColumnOption);
        Add(_conditionsOption);
        Add(_elseValueOption);

        // Set the handler
        this.SetAction(async (parseResult) =>
        {
            var inputPath = parseResult.GetValue(_inputOption)!;
            var outputPath = parseResult.GetValue(_outputOption)!;
            var outputColumn = parseResult.GetValue(_outputColumnOption)!;
            var conditions = parseResult.GetValue(_conditionsOption)!;
            var elseValue = parseResult.GetValue(_elseValueOption)!;
            var hasHeader = parseResult.GetValue(CommonOptions.HasHeader);
            var ignoreErrors = parseResult.GetValue(CommonOptions.IgnoreErrors);
            var verbose = parseResult.GetValue(CommonOptions.Verbose);

            return await ExecuteAsync(
                inputPath, outputPath, outputColumn, conditions, elseValue,
                hasHeader, ignoreErrors, verbose);
        });
    }

    private async Task<int> ExecuteAsync(
        string inputPath, string outputPath, string outputColumn,
        string[] conditions, string elseValue,
        bool hasHeader, bool ignoreErrors, bool verbose)
    {
        try
        {
            // Validation with rich output
            var validationResults = new List<(string Name, bool IsValid, string? Error)>
            {
                ("Input file", ValidateInputFile(inputPath, out var inputError), inputError),
                ("Output directory", ValidateOutputPath(outputPath), ValidateOutputPath(outputPath) ? null : "Directory not found"),
                ("Output column", !string.IsNullOrWhiteSpace(outputColumn), string.IsNullOrWhiteSpace(outputColumn) ? "Output column name required" : $"'{outputColumn}'"),
                ("Conditions", conditions.Length > 0, conditions.Length > 0 ? $"{conditions.Length} condition(s)" : "At least one condition required"),
            };

            // Validate condition format
            for (int i = 0; i < conditions.Length; i++)
            {
                var isValid = conditions[i].Contains(':');
                validationResults.Add((
                    $"Condition {i + 1}",
                    isValid,
                    isValid ? $"{conditions[i].Substring(0, Math.Min(50, conditions[i].Length))}{(conditions[i].Length > 50 ? "..." : "")}" : "Invalid format (expected 'Column operator Value : ResultValue')"
                ));
            }

            if (!string.IsNullOrEmpty(elseValue))
            {
                validationResults.Add(("Else value", true, $"'{elseValue}'"));
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
            var options = new ConditionalOption
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                OutputColumn = outputColumn,
                Conditions = conditions.ToList(),
                ElseValue = elseValue,
                HasHeader = hasHeader,
                IgnoreErrors = ignoreErrors
            };

            // Execute with progress display
            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Creating conditional column...", async ctx =>
                {
                    ctx.Status("Reading input file...");

                    var taskLogger = LoggerFactory.CreateLogger<ConditionalTask>();
                    var task = new ConditionalTask(taskLogger);
                    var taskContext = new TaskContext(options);

                    Logger.LogInformation("Processing conditional column creation");
                    Logger.LogInformation("  Output column: {Column}", outputColumn);

                    if (verbose)
                    {
                        Logger.LogInformation("  Conditions:");
                        for (int i = 0; i < conditions.Length; i++)
                        {
                            Logger.LogInformation("    {Index}. {Condition}", i + 1, conditions[i]);
                        }

                        if (!string.IsNullOrEmpty(elseValue))
                        {
                            Logger.LogInformation("  Else value: {Else}", elseValue);
                        }
                    }

                    ctx.Status("Evaluating conditions...");
                    var success = await task.ExecuteAsync(taskContext);

                    if (success)
                    {
                        DisplaySuccess($"Conditional column created successfully: {outputPath}");

                        if (verbose)
                        {
                            var summaryText = $"""
                                [bold]Summary:[/]
                                • Input: {Markup.Escape(inputPath)}
                                • Output: {Markup.Escape(outputPath)}
                                • New column: {outputColumn}
                                • Conditions: {conditions.Length}
                                • Else value: {(string.IsNullOrEmpty(elseValue) ? "(empty)" : elseValue)}
                                • Has header: {hasHeader}
                                """;

                            var panel = new Panel(new Markup(summaryText))
                            {
                                Header = new PanelHeader("Conditional Column Complete", Justify.Center),
                                BorderStyle = new Style(Color.Green)
                            };
                            AnsiConsole.Write(panel);
                        }

                        return ExitCodes.Success;
                    }

                    DisplayError("Failed to create conditional column");
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
