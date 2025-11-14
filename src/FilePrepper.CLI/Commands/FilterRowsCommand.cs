using System.CommandLine;
using FilePrepper.Tasks;
using FilePrepper.Tasks.FilterRows;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

/// <summary>
/// Command to filter rows based on conditions
/// </summary>
public class FilterRowsCommand : BaseCommand
{
    private readonly Option<string> _inputOption;
    private readonly Option<string> _outputOption;
    private readonly Option<string[]> _conditionsOption;

    public FilterRowsCommand(ILoggerFactory loggerFactory)
        : base("filter-rows", "Filter rows based on column conditions (e.g., Age:GreaterThan:30)", loggerFactory)
    {
        // Required options
        _inputOption = new Option<string>("--input", new[] { "-i" }) { Description = "Input file path", Required = true };

        _outputOption = new Option<string>("--output", new[] { "-o" }) { Description = "Output file path", Required = true };

        _conditionsOption = new Option<string[]>("--conditions", new[] { "-c" }) { Description = "Filter conditions in format column:operator:value (e.g., Age:GreaterThan:30). Operators: Equals, NotEquals, GreaterThan, GreaterOrEqual, LessThan, LessOrEqual, Contains, NotContains, StartsWith, EndsWith", CustomParser = result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries), Required = true };

        // Add all options
        Add(_inputOption);
        Add(_outputOption);
        Add(_conditionsOption);

        // Set the handler
        this.SetAction(async (parseResult) =>
        {
            var inputPath = parseResult.GetValue(_inputOption)!;
            var outputPath = parseResult.GetValue(_outputOption)!;
            var conditions = parseResult.GetValue(_conditionsOption)!;
            var hasHeader = parseResult.GetValue(CommonOptions.HasHeader);
            var ignoreErrors = parseResult.GetValue(CommonOptions.IgnoreErrors);
            var verbose = parseResult.GetValue(CommonOptions.Verbose);

            return await ExecuteAsync(
                inputPath, outputPath, conditions,
                hasHeader, ignoreErrors, verbose);
        });
    }

    private async Task<int> ExecuteAsync(
        string inputPath, string outputPath, string[] conditionStrings,
        bool hasHeader, bool ignoreErrors, bool verbose)
    {
        try
        {
            // Validation with rich output
            var inputValid = ValidateInputFile(inputPath, out var inputError);

            var validationResults = new List<(string Name, bool IsValid, string? Error)>
            {
                ("Input file", inputValid, inputError),
                ("Input format", inputValid, inputValid ? $"{GetFormatName(inputPath)} format" : null),
                ("Output directory", ValidateOutputPath(outputPath), ValidateOutputPath(outputPath) ? null : "Directory not found"),
                ("Conditions", conditionStrings.Length > 0, conditionStrings.Length > 0 ? null : "At least one condition required")
            };

            // Parse and validate conditions
            var conditions = new List<FilterCondition>();
            var conditionErrors = new List<string>();

            foreach (var condStr in conditionStrings)
            {
                var parts = condStr.Split(':');
                if (parts.Length != 3)
                {
                    conditionErrors.Add($"Invalid format: '{condStr}' (expected column:operator:value)");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(parts[0]))
                {
                    conditionErrors.Add($"Empty column name in: '{condStr}'");
                    continue;
                }

                if (!Enum.TryParse<FilterOperator>(parts[1], true, out var filterOperator))
                {
                    var validOperators = string.Join(", ", Enum.GetNames<FilterOperator>());
                    conditionErrors.Add($"Invalid operator '{parts[1]}' (valid: {validOperators})");
                    continue;
                }

                conditions.Add(new FilterCondition
                {
                    ColumnName = parts[0],
                    Operator = filterOperator,
                    Value = parts[2]
                });
            }

            if (conditionErrors.Any())
            {
                validationResults.Add(("Condition parsing", false, string.Join("; ", conditionErrors)));
            }
            else
            {
                validationResults.Add(("Condition parsing", true, $"{conditions.Count} condition(s) parsed"));
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
            var options = new FilterRowsOption
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                Conditions = conditions,
                HasHeader = hasHeader,
                IgnoreErrors = ignoreErrors
            };

            // Execute with progress display
            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Filtering rows...", async ctx =>
                {
                    ctx.Status($"Reading input from {Path.GetFileName(inputPath)}...");

                    var taskLogger = LoggerFactory.CreateLogger<FilterRowsTask>();
                    var task = new FilterRowsTask(taskLogger);
                    var taskContext = new TaskContext(options);

                    Logger.LogInformation(
                        "Filtering rows: {ConditionCount} condition(s) applied",
                        conditions.Count);

                    if (verbose)
                    {
                        foreach (var cond in conditions)
                        {
                            Logger.LogInformation(
                                "  Condition: {Column} {Operator} {Value}",
                                cond.ColumnName, cond.Operator, cond.Value);
                        }
                    }

                    ctx.Status("Applying filters...");
                    var success = await task.ExecuteAsync(taskContext);

                    if (success)
                    {
                        DisplaySuccess($"Rows filtered successfully: {outputPath}");

                        if (verbose)
                        {
                            var panel = new Panel(
                                new Markup($"""
                                    [bold]Summary:[/]
                                    • Input: {Markup.Escape(inputPath)}
                                    • Output: {Markup.Escape(outputPath)}
                                    • Conditions: {conditions.Count}
                                    • Has header: {hasHeader}
                                    """))
                            {
                                Header = new PanelHeader("Filter Rows Complete", Justify.Center),
                                BorderStyle = new Style(Color.Green)
                            };
                            AnsiConsole.Write(panel);
                        }

                        return ExitCodes.Success;
                    }

                    DisplayError("Failed to filter rows");
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
