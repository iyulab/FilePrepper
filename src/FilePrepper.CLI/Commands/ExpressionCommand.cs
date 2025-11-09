using System.CommandLine;
using FilePrepper.Tasks;
using FilePrepper.Tasks.Expression;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

/// <summary>
/// Command to create computed columns from expressions
/// </summary>
public class ExpressionCommand : BaseCommand
{
    private readonly Option<string> _inputOption;
    private readonly Option<string> _outputOption;
    private readonly Option<string[]> _expressionsOption;
    private readonly Option<bool> _removeSourceOption;

    public ExpressionCommand(ILoggerFactory loggerFactory)
        : base("expression", "Create computed columns from arithmetic expressions", loggerFactory)
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

        _expressionsOption = new Option<string[]>(
            aliases: new[] { "--expressions", "-e" },
            description: "Column expressions (format: 'output=expression' or 'output=expression@position', space-separated)")
        { IsRequired = true, AllowMultipleArgumentsPerToken = true };

        // Optional options
        _removeSourceOption = new Option<bool>(
            aliases: new[] { "--remove-source", "-r" },
            getDefaultValue: () => false,
            description: "Remove source columns used in expressions");

        // Add all options
        AddOption(_inputOption);
        AddOption(_outputOption);
        AddOption(_expressionsOption);
        AddOption(_removeSourceOption);

        // Set the handler
        this.SetHandler(async (context) =>
        {
            var inputPath = context.ParseResult.GetValueForOption(_inputOption)!;
            var outputPath = context.ParseResult.GetValueForOption(_outputOption)!;
            var expressions = context.ParseResult.GetValueForOption(_expressionsOption)!;
            var removeSource = context.ParseResult.GetValueForOption(_removeSourceOption);
            var hasHeader = context.ParseResult.GetValueForOption(CommonOptions.HasHeader);
            var ignoreErrors = context.ParseResult.GetValueForOption(CommonOptions.IgnoreErrors);
            var verbose = context.ParseResult.GetValueForOption(CommonOptions.Verbose);

            context.ExitCode = await ExecuteAsync(
                inputPath, outputPath, expressions, removeSource,
                hasHeader, ignoreErrors, verbose);
        });
    }

    private async Task<int> ExecuteAsync(
        string inputPath, string outputPath, string[] expressions, bool removeSource,
        bool hasHeader, bool ignoreErrors, bool verbose)
    {
        try
        {
            // Parse expressions
            List<ColumnExpression> parsedExpressions;
            try
            {
                parsedExpressions = expressions.Select(ColumnExpression.Parse).ToList();
            }
            catch (Exception ex)
            {
                DisplayError($"Invalid expression format: {ex.Message}");
                DisplayInfo("Expression format: 'output=expression' or 'output=expression@position'");
                DisplayInfo("Example: 'total=price*quantity' or 'gap=required-stock@0'");
                return ExitCodes.InvalidArguments;
            }

            // Validation with rich output
            var validationResults = new List<(string Name, bool IsValid, string? Error)>
            {
                ("Input file", ValidateInputFile(inputPath, out var inputError), inputError),
                ("Output directory", ValidateOutputPath(outputPath), ValidateOutputPath(outputPath) ? null : "Directory not found"),
                ("Expressions", parsedExpressions.Count > 0, parsedExpressions.Count > 0 ? $"{parsedExpressions.Count} expression(s)" : "At least 1 expression required"),
            };

            // Validate each expression
            foreach (var (expr, index) in parsedExpressions.Select((e, i) => (e, i)))
            {
                var isValid = expr.IsValid;
                validationResults.Add((
                    $"Expression {index + 1}",
                    isValid,
                    isValid ? expr.ToString() : "Invalid expression"
                ));
            }

            if (removeSource)
            {
                validationResults.Add(("Remove source", true, "Will remove source columns used in expressions"));
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
            var options = new ExpressionOption
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                Expressions = parsedExpressions,
                RemoveSourceColumns = removeSource,
                HasHeader = hasHeader,
                IgnoreErrors = ignoreErrors
            };

            // Execute with progress display
            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Evaluating expressions...", async ctx =>
                {
                    ctx.Status("Reading input file...");

                    var taskLogger = LoggerFactory.CreateLogger<ExpressionTask>();
                    var task = new ExpressionTask(taskLogger);
                    var taskContext = new TaskContext(options);

                    Logger.LogInformation("Evaluating {Count} expression(s)", parsedExpressions.Count);

                    if (verbose)
                    {
                        foreach (var expr in parsedExpressions)
                        {
                            Logger.LogInformation("  {Expression}", expr.ToString());
                        }
                    }

                    ctx.Status("Computing column expressions...");
                    var success = await task.ExecuteAsync(taskContext);

                    if (success)
                    {
                        DisplaySuccess($"Expressions evaluated successfully: {outputPath}");

                        if (verbose)
                        {
                            var summaryText = $"""
                                [bold]Summary:[/]
                                • Input: {Markup.Escape(inputPath)}
                                • Output: {Markup.Escape(outputPath)}
                                • Expressions: {parsedExpressions.Count}
                                • Remove source: {removeSource}
                                • Has header: {hasHeader}
                                """;

                            var panel = new Panel(new Markup(summaryText))
                            {
                                Header = new PanelHeader("Expression Evaluation Complete", Justify.Center),
                                BorderStyle = new Style(Color.Green)
                            };
                            AnsiConsole.Write(panel);
                        }

                        return ExitCodes.Success;
                    }

                    DisplayError("Failed to evaluate expressions");
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
