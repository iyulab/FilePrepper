using System.CommandLine;
using FilePrepper.Tasks;
using FilePrepper.Tasks.Aggregate;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

/// <summary>
/// Command to aggregate data based on group by columns
/// </summary>
public class AggregateCommand : BaseCommand
{
    private readonly Option<string> _inputOption;
    private readonly Option<string> _outputOption;
    private readonly Option<string[]> _groupByOption;
    private readonly Option<string[]> _aggregatesOption;
    private readonly Option<bool> _appendOption;
    private readonly Option<string?> _templateOption;

    public AggregateCommand(ILoggerFactory loggerFactory)
        : base("aggregate", "Aggregate data based on group by columns", loggerFactory)
    {
        _inputOption = new Option<string>(
            aliases: new[] { "--input", "-i" },
            description: "Input file path")
        { IsRequired = true };

        _outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            description: "Output file path")
        { IsRequired = true };

        _groupByOption = new Option<string[]>(
            aliases: new[] { "--group-by", "-g" },
            description: "Columns to group by",
            parseArgument: result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
        { IsRequired = true };

        _aggregatesOption = new Option<string[]>(
            aliases: new[] { "--aggregates", "-a" },
            description: "Aggregate functions in format column:function:output (e.g. Sales:Sum:TotalSales). Functions: Sum, Average, Count, Min, Max",
            parseArgument: result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
        { IsRequired = true };

        _appendOption = new Option<bool>(
            aliases: new[] { "--append-to-source" },
            description: "Append result to source file",
            getDefaultValue: () => false);

        _templateOption = new Option<string?>(
            aliases: new[] { "--output-column" },
            description: "Output column name template when appending");

        AddOption(_inputOption);
        AddOption(_outputOption);
        AddOption(_groupByOption);
        AddOption(_aggregatesOption);
        AddOption(_appendOption);
        AddOption(_templateOption);

        this.SetHandler(async (context) =>
        {
            var inputPath = context.ParseResult.GetValueForOption(_inputOption)!;
            var outputPath = context.ParseResult.GetValueForOption(_outputOption)!;
            var groupBy = context.ParseResult.GetValueForOption(_groupByOption)!;
            var aggregates = context.ParseResult.GetValueForOption(_aggregatesOption)!;
            var append = context.ParseResult.GetValueForOption(_appendOption);
            var template = context.ParseResult.GetValueForOption(_templateOption);
            var hasHeader = context.ParseResult.GetValueForOption(CommonOptions.HasHeader);
            var ignoreErrors = context.ParseResult.GetValueForOption(CommonOptions.IgnoreErrors);
            var verbose = context.ParseResult.GetValueForOption(CommonOptions.Verbose);

            context.ExitCode = await ExecuteAsync(
                inputPath, outputPath, groupBy, aggregates, append, template,
                hasHeader, ignoreErrors, verbose);
        });
    }

    private async Task<int> ExecuteAsync(
        string inputPath, string outputPath, string[] groupByColumns, string[] aggregateStrings,
        bool appendToSource, string? outputColumnTemplate,
        bool hasHeader, bool ignoreErrors, bool verbose)
    {
        try
        {
            // Parse aggregate definitions
            var aggregateColumns = new List<AggregateColumn>();
            var aggErrors = new List<string>();

            foreach (var aggStr in aggregateStrings)
            {
                var parts = aggStr.Split(':');
                if (parts.Length != 3)
                {
                    aggErrors.Add($"Invalid format: '{aggStr}' (expected column:function:output)");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[2]))
                {
                    aggErrors.Add($"Column names cannot be empty in: '{aggStr}'");
                    continue;
                }

                if (!Enum.TryParse<AggregateFunction>(parts[1], true, out var function))
                {
                    var validFunctions = string.Join(", ", Enum.GetNames<AggregateFunction>());
                    aggErrors.Add($"Invalid function '{parts[1]}' (valid: {validFunctions})");
                    continue;
                }

                aggregateColumns.Add(new AggregateColumn
                {
                    ColumnName = parts[0],
                    Function = function,
                    OutputColumnName = parts[2]
                });
            }

            // Validation
            var inputValid = ValidateInputFile(inputPath, out var inputError);
            var groupByValid = groupByColumns.All(c => !string.IsNullOrWhiteSpace(c));

            var validationResults = new List<(string Name, bool IsValid, string? Error)>
            {
                ("Input file", inputValid, inputError),
                ("Output directory", ValidateOutputPath(outputPath), null),
                ("Group by columns", groupByValid, groupByValid ? null : "Empty column names found"),
                ("Aggregates", aggErrors.Count == 0, aggErrors.Any() ? string.Join("; ", aggErrors) : null)
            };

            if (aggErrors.Count == 0)
            {
                validationResults.Add(("Aggregate parsing", true, $"{aggregateColumns.Count} aggregate(s) parsed"));
            }

            if (appendToSource && string.IsNullOrWhiteSpace(outputColumnTemplate))
            {
                validationResults.Add(("Output template", false, "Required when appending to source"));
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
            var options = new AggregateOption
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                GroupByColumns = groupByColumns,
                AggregateColumns = aggregateColumns,
                AppendToSource = appendToSource,
                OutputColumnTemplate = outputColumnTemplate,
                HasHeader = hasHeader,
                IgnoreErrors = ignoreErrors
            };

            // Execute
            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Aggregating data...", async ctx =>
                {
                    var taskLogger = LoggerFactory.CreateLogger<AggregateTask>();
                    var task = new AggregateTask(taskLogger);
                    var taskContext = new TaskContext(options);

                    Logger.LogInformation(
                        "Aggregating: {GroupByCount} group by column(s), {AggCount} aggregate(s)",
                        groupByColumns.Length, aggregateColumns.Count);

                    var success = await task.ExecuteAsync(taskContext);

                    if (success)
                    {
                        DisplaySuccess($"Data aggregated successfully: {outputPath}");

                        if (verbose)
                        {
                            var panel = new Panel(
                                new Markup($"""
                                    [bold]Summary:[/]
                                    • Input: {Markup.Escape(inputPath)}
                                    • Output: {Markup.Escape(outputPath)}
                                    • Group by: {string.Join(", ", groupByColumns)}
                                    • Aggregates: {aggregateColumns.Count}
                                    """))
                            {
                                Header = new PanelHeader("Aggregate Complete", Justify.Center),
                                BorderStyle = new Style(Color.Green)
                            };
                            AnsiConsole.Write(panel);
                        }

                        return ExitCodes.Success;
                    }

                    DisplayError("Failed to aggregate data");
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
