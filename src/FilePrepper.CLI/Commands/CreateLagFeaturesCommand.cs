using System.CommandLine;
using FilePrepper.Tasks;
using FilePrepper.Tasks.CreateLagFeatures;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

/// <summary>
/// Command to create lag features from time series data
/// </summary>
public class CreateLagFeaturesCommand : BaseCommand
{
    private readonly Option<string> _inputOption;
    private readonly Option<string> _outputOption;
    private readonly Option<string> _groupByOption;
    private readonly Option<string> _timeColumnOption;
    private readonly Option<string[]> _lagColumnsOption;
    private readonly Option<int[]> _lagPeriodsOption;
    private readonly Option<string?> _targetColumnOption;
    private readonly Option<bool> _dropNullsOption;
    private readonly Option<string[]?> _keepColumnsOption;

    public CreateLagFeaturesCommand(ILoggerFactory loggerFactory)
        : base("create-lag-features", "Create lag features from time series data for machine learning", loggerFactory)
    {
        // Required options
        _inputOption = new Option<string>("--input", new[] { "-i" }) { Description = "Input CSV file path", Required = true };

        _outputOption = new Option<string>("--output", new[] { "-o" }) { Description = "Output file path", Required = true };

        _groupByOption = new Option<string>("--group-by", new[] { "-g" }) { Description = "Column to group by (e.g., Part Number, Entity ID)", Required = true };

        _timeColumnOption = new Option<string>("--time-column", new[] { "-t" }) { Description = "Column representing time/sequence for sorting within groups", Required = true };

        _lagColumnsOption = new Option<string[]>("--lag-columns", new[] { "-l" }) { Description = "Comma-separated list of columns to create lag features from", CustomParser = result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries), Required = true };

        _lagPeriodsOption = new Option<int[]>("--lag-periods", new[] { "-p" })
        {
            Description = "Comma-separated list of lag periods (e.g., 1,2,3)",
            CustomParser = result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray(),
            Required = true
        };

        // Optional options
        _targetColumnOption = new Option<string?>("--target") { Description = "Target column to predict (optional, will be kept in output)" };

        _dropNullsOption = new Option<bool>("--drop-nulls") { Description = "Drop rows with null lag values", DefaultValueFactory = _ => true };

        _keepColumnsOption = new Option<string[]?>("--keep-columns", new[] { "-k" })
        {
            Description = "Comma-separated list of additional columns to keep in output",
            CustomParser = result =>
            {
                if (result.Tokens.Count == 0) return null;
                return result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries);
            }
        };

        // Add all options
        Add(_inputOption);
        Add(_outputOption);
        Add(_groupByOption);
        Add(_timeColumnOption);
        Add(_lagColumnsOption);
        Add(_lagPeriodsOption);
        Add(_targetColumnOption);
        Add(_dropNullsOption);
        Add(_keepColumnsOption);

        // Set the handler (System.CommandLine max 8 parameters, use context for common options)
        this.SetAction(async (parseResult) =>
        {
            var inputPath = parseResult.GetValue(_inputOption)!;
            var outputPath = parseResult.GetValue(_outputOption)!;
            var groupBy = parseResult.GetValue(_groupByOption)!;
            var timeColumn = parseResult.GetValue(_timeColumnOption)!;
            var lagColumns = parseResult.GetValue(_lagColumnsOption)!;
            var lagPeriods = parseResult.GetValue(_lagPeriodsOption)!;
            var targetColumn = parseResult.GetValue(_targetColumnOption);
            var dropNulls = parseResult.GetValue(_dropNullsOption);
            var keepColumns = parseResult.GetValue(_keepColumnsOption);
            var hasHeader = parseResult.GetValue(CommonOptions.HasHeader);
            var ignoreErrors = parseResult.GetValue(CommonOptions.IgnoreErrors);
            var verbose = parseResult.GetValue(CommonOptions.Verbose);

            return await ExecuteAsync(
                inputPath, outputPath, groupBy, timeColumn,
                lagColumns, lagPeriods, targetColumn,
                dropNulls, keepColumns,
                hasHeader, ignoreErrors, verbose);
        });
    }

    private async Task<int> ExecuteAsync(
        string inputPath, string outputPath, string groupBy, string timeColumn,
        string[] lagColumns, int[] lagPeriods, string? targetColumn,
        bool dropNulls, string[]? keepColumns,
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
                ("Lag columns", lagColumns.Length > 0, lagColumns.Length > 0 ? null : "At least one column required"),
                ("Lag periods", lagPeriods.Length > 0 && lagPeriods.All(p => p > 0), lagPeriods.All(p => p > 0) ? null : "All periods must be positive")
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
            var options = new CreateLagFeaturesOption
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                GroupByColumn = groupBy,
                TimeColumn = timeColumn,
                LagColumns = lagColumns.ToList(),
                LagPeriods = lagPeriods.ToList(),
                TargetColumn = targetColumn,
                DropNullRows = dropNulls,
                KeepColumns = keepColumns?.ToList() ?? new List<string>(),
                HasHeader = hasHeader,
                IgnoreErrors = ignoreErrors
            };

            // Execute with progress display
            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Processing time series data...", async ctx =>
                {
                    ctx.Status($"Reading input from {Path.GetFileName(inputPath)}...");
                    
                    var taskLogger = LoggerFactory.CreateLogger<CreateLagFeaturesTask>();
                    var task = new CreateLagFeaturesTask(taskLogger);
                    var taskContext = new TaskContext(options);

                    Logger.LogInformation(
                        "Creating lag features: {LagColumns} columns × {LagPeriods} periods",
                        lagColumns.Length, lagPeriods.Length);

                    ctx.Status("Creating lag features...");
                    var success = await task.ExecuteAsync(taskContext);

                    if (success)
                    {
                        DisplaySuccess($"Lag features created successfully: {outputPath}");
                        
                        if (verbose)
                        {
                            var panel = new Panel(
                                new Markup($"""
                                    [bold]Summary:[/]
                                    • Input: {Markup.Escape(inputPath)}
                                    • Output: {Markup.Escape(outputPath)}
                                    • Group by: {Markup.Escape(groupBy)}
                                    • Time column: {Markup.Escape(timeColumn)}
                                    • Lag columns: {lagColumns.Length}
                                    • Lag periods: {string.Join(", ", lagPeriods)}
                                    • Features created: {lagColumns.Length * lagPeriods.Length}
                                    """))
                            {
                                Header = new PanelHeader("Lag Features Creation Complete", Justify.Center),
                                BorderStyle = new Style(Color.Green)
                            };
                            AnsiConsole.Write(panel);
                        }
                        
                        return ExitCodes.Success;
                    }

                    DisplayError("Failed to create lag features");
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
