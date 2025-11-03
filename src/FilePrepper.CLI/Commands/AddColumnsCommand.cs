using System.CommandLine;
using FilePrepper.Tasks;
using FilePrepper.Tasks.AddColumns;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

/// <summary>
/// Command to add new columns to the CSV file with specified values
/// </summary>
public class AddColumnsCommand : BaseCommand
{
    private readonly Option<string> _inputOption;
    private readonly Option<string> _outputOption;
    private readonly Option<string[]> _columnsOption;

    public AddColumnsCommand(ILoggerFactory loggerFactory)
        : base("add-columns", "Add new columns to the CSV file with specified values", loggerFactory)
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

        _columnsOption = new Option<string[]>(
            aliases: new[] { "--columns", "-c" },
            description: "Columns to add in format name=value (e.g. Age=30,City=Seoul)",
            parseArgument: result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
        { IsRequired = true };

        // Add all options
        AddOption(_inputOption);
        AddOption(_outputOption);
        AddOption(_columnsOption);

        // Set the handler
        this.SetHandler(async (context) =>
        {
            var inputPath = context.ParseResult.GetValueForOption(_inputOption)!;
            var outputPath = context.ParseResult.GetValueForOption(_outputOption)!;
            var columns = context.ParseResult.GetValueForOption(_columnsOption)!;
            var hasHeader = context.ParseResult.GetValueForOption(CommonOptions.HasHeader);
            var ignoreErrors = context.ParseResult.GetValueForOption(CommonOptions.IgnoreErrors);
            var verbose = context.ParseResult.GetValueForOption(CommonOptions.Verbose);

            context.ExitCode = await ExecuteAsync(
                inputPath, outputPath, columns,
                hasHeader, ignoreErrors, verbose);
        });
    }

    private async Task<int> ExecuteAsync(
        string inputPath, string outputPath, string[] columnStrings,
        bool hasHeader, bool ignoreErrors, bool verbose)
    {
        try
        {
            // Parse column definitions
            var newColumns = new Dictionary<string, string>();
            var columnErrors = new List<string>();

            foreach (var colStr in columnStrings)
            {
                var parts = colStr.Split('=', 2);
                if (parts.Length != 2)
                {
                    columnErrors.Add($"Invalid format: '{colStr}' (expected name=value)");
                    continue;
                }

                var name = parts[0].Trim();
                var value = parts[1].Trim();

                if (string.IsNullOrWhiteSpace(name))
                {
                    columnErrors.Add($"Empty column name in: '{colStr}'");
                    continue;
                }

                if (newColumns.ContainsKey(name))
                {
                    columnErrors.Add($"Duplicate column name: '{name}'");
                    continue;
                }

                newColumns[name] = value;
            }

            // Validation with rich output
            var inputValid = ValidateInputFile(inputPath, out var inputError);

            var validationResults = new List<(string Name, bool IsValid, string? Error)>
            {
                ("Input file", inputValid, inputError),
                ("Input format", inputValid, inputValid ? $"{GetFormatName(inputPath)} format" : null),
                ("Output directory", ValidateOutputPath(outputPath), ValidateOutputPath(outputPath) ? null : "Directory not found"),
                ("Columns", columnStrings.Length > 0 && columnErrors.Count == 0,
                    columnErrors.Any() ? string.Join("; ", columnErrors) : null)
            };

            if (columnErrors.Count == 0)
            {
                validationResults.Add(("Column parsing", true, $"{newColumns.Count} column(s) parsed"));
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
            var options = new AddColumnsOption
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                NewColumns = newColumns,
                HasHeader = hasHeader,
                IgnoreErrors = ignoreErrors
            };

            // Execute with progress display
            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Adding columns...", async ctx =>
                {
                    ctx.Status($"Reading input from {Path.GetFileName(inputPath)}...");

                    var taskLogger = LoggerFactory.CreateLogger<AddColumnsTask>();
                    var task = new AddColumnsTask(taskLogger);
                    var taskContext = new TaskContext(options);

                    Logger.LogInformation(
                        "Adding {ColumnCount} column(s)",
                        newColumns.Count);

                    if (verbose)
                    {
                        foreach (var (name, value) in newColumns)
                        {
                            Logger.LogInformation("  {Name} = {Value}", name, value);
                        }
                    }

                    ctx.Status("Adding columns...");
                    var success = await task.ExecuteAsync(taskContext);

                    if (success)
                    {
                        DisplaySuccess($"Columns added successfully: {outputPath}");

                        if (verbose)
                        {
                            var panel = new Panel(
                                new Markup($"""
                                    [bold]Summary:[/]
                                    • Input: {Markup.Escape(inputPath)}
                                    • Output: {Markup.Escape(outputPath)}
                                    • Columns added: {newColumns.Count}
                                    • Has header: {hasHeader}
                                    """))
                            {
                                Header = new PanelHeader("Add Columns Complete", Justify.Center),
                                BorderStyle = new Style(Color.Green)
                            };
                            AnsiConsole.Write(panel);
                        }

                        return ExitCodes.Success;
                    }

                    DisplayError("Failed to add columns");
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
