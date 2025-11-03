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
        _inputOption = new Option<string>(
            aliases: new[] { "--input", "-i" },
            description: "Input CSV file path")
        { IsRequired = true };

        _outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            description: "Output file path")
        { IsRequired = true };

        _groupByOption = new Option<string>(
            aliases: new[] { "--group-by", "-g" },
            description: "Column to group by (e.g., Part Number, Entity ID)")
        { IsRequired = true };

        _timeColumnOption = new Option<string>(
            aliases: new[] { "--time-column", "-t" },
            description: "Column representing time/sequence for sorting within groups")
        { IsRequired = true };

        _lagColumnsOption = new Option<string[]>(
            aliases: new[] { "--lag-columns", "-l" },
            description: "Comma-separated list of columns to create lag features from",
            parseArgument: result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
        { IsRequired = true };

        _lagPeriodsOption = new Option<int[]>(
            aliases: new[] { "--lag-periods", "-p" },
            description: "Comma-separated list of lag periods (e.g., 1,2,3)",
            parseArgument: result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray())
        { IsRequired = true };

        // Optional options
        _targetColumnOption = new Option<string?>(
            aliases: new[] { "--target" },
            description: "Target column to predict (optional, will be kept in output)");

        _dropNullsOption = new Option<bool>(
            aliases: new[] { "--drop-nulls" },
            getDefaultValue: () => true,
            description: "Drop rows with null lag values");

        _keepColumnsOption = new Option<string[]?>(
            aliases: new[] { "--keep-columns", "-k" },
            description: "Comma-separated list of additional columns to keep in output",
            parseArgument: result =>
            {
                if (result.Tokens.Count == 0) return null;
                return result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries);
            });

        // Add all options
        AddOption(_inputOption);
        AddOption(_outputOption);
        AddOption(_groupByOption);
        AddOption(_timeColumnOption);
        AddOption(_lagColumnsOption);
        AddOption(_lagPeriodsOption);
        AddOption(_targetColumnOption);
        AddOption(_dropNullsOption);
        AddOption(_keepColumnsOption);

        // Set the handler (System.CommandLine max 8 parameters, use context for common options)
        this.SetHandler(async (context) =>
        {
            var inputPath = context.ParseResult.GetValueForOption(_inputOption)!;
            var outputPath = context.ParseResult.GetValueForOption(_outputOption)!;
            var groupBy = context.ParseResult.GetValueForOption(_groupByOption)!;
            var timeColumn = context.ParseResult.GetValueForOption(_timeColumnOption)!;
            var lagColumns = context.ParseResult.GetValueForOption(_lagColumnsOption)!;
            var lagPeriods = context.ParseResult.GetValueForOption(_lagPeriodsOption)!;
            var targetColumn = context.ParseResult.GetValueForOption(_targetColumnOption);
            var dropNulls = context.ParseResult.GetValueForOption(_dropNullsOption);
            var keepColumns = context.ParseResult.GetValueForOption(_keepColumnsOption);
            var hasHeader = context.ParseResult.GetValueForOption(CommonOptions.HasHeader);
            var ignoreErrors = context.ParseResult.GetValueForOption(CommonOptions.IgnoreErrors);
            var verbose = context.ParseResult.GetValueForOption(CommonOptions.Verbose);

            context.ExitCode = await ExecuteAsync(
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
