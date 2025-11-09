using System.CommandLine;
using FilePrepper.Tasks;
using FilePrepper.Tasks.Unpivot;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

/// <summary>
/// Command to unpivot (wide-to-long) data transformation
/// </summary>
public class UnpivotCommand : BaseCommand
{
    private readonly Option<string> _inputOption;
    private readonly Option<string> _outputOption;
    private readonly Option<string[]> _baseColumnsOption;
    private readonly Option<string[]> _columnGroupsOption;
    private readonly Option<string> _indexColumnOption;
    private readonly Option<string[]> _valueColumnsOption;
    private readonly Option<bool> _skipEmptyOption;

    public UnpivotCommand(ILoggerFactory loggerFactory)
        : base("unpivot", "Transform wide format data to long format (unpivot)", loggerFactory)
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

        _columnGroupsOption = new Option<string[]>(
            aliases: new[] { "--column-groups", "-g" },
            description: "Column groups to unpivot (space-separated)")
        { IsRequired = true, AllowMultipleArgumentsPerToken = true };

        _valueColumnsOption = new Option<string[]>(
            aliases: new[] { "--value-columns", "-vc" },
            description: "Names for value columns in output (space-separated)")
        { IsRequired = true, AllowMultipleArgumentsPerToken = true };

        // Optional options
        _baseColumnsOption = new Option<string[]>(
            aliases: new[] { "--base-columns", "-b" },
            getDefaultValue: () => Array.Empty<string>(),
            description: "Base columns to keep in every output row (space-separated)")
        { AllowMultipleArgumentsPerToken = true };

        _indexColumnOption = new Option<string>(
            aliases: new[] { "--index-column", "-idx" },
            getDefaultValue: () => "Index",
            description: "Name for the index column in output");

        _skipEmptyOption = new Option<bool>(
            aliases: new[] { "--skip-empty", "-se" },
            getDefaultValue: () => true,
            description: "Skip rows where all value columns are empty");

        // Add all options
        AddOption(_inputOption);
        AddOption(_outputOption);
        AddOption(_baseColumnsOption);
        AddOption(_columnGroupsOption);
        AddOption(_indexColumnOption);
        AddOption(_valueColumnsOption);
        AddOption(_skipEmptyOption);

        // Set the handler
        this.SetHandler(async (context) =>
        {
            var inputPath = context.ParseResult.GetValueForOption(_inputOption)!;
            var outputPath = context.ParseResult.GetValueForOption(_outputOption)!;
            var baseColumns = context.ParseResult.GetValueForOption(_baseColumnsOption) ?? Array.Empty<string>();
            var columnGroups = context.ParseResult.GetValueForOption(_columnGroupsOption)!;
            var indexColumn = context.ParseResult.GetValueForOption(_indexColumnOption)!;
            var valueColumns = context.ParseResult.GetValueForOption(_valueColumnsOption)!;
            var skipEmpty = context.ParseResult.GetValueForOption(_skipEmptyOption);
            var hasHeader = context.ParseResult.GetValueForOption(CommonOptions.HasHeader);
            var ignoreErrors = context.ParseResult.GetValueForOption(CommonOptions.IgnoreErrors);
            var verbose = context.ParseResult.GetValueForOption(CommonOptions.Verbose);

            context.ExitCode = await ExecuteAsync(
                inputPath, outputPath, baseColumns, columnGroups, indexColumn, valueColumns, skipEmpty,
                hasHeader, ignoreErrors, verbose);
        });
    }

    private async Task<int> ExecuteAsync(
        string inputPath, string outputPath, string[] baseColumns, string[] columnGroups,
        string indexColumn, string[] valueColumns, bool skipEmpty,
        bool hasHeader, bool ignoreErrors, bool verbose)
    {
        try
        {
            // Parse column groups - each group should have the same number of columns
            // Assume columnGroups contains all columns sequentially grouped by number of value columns
            if (columnGroups.Length % valueColumns.Length != 0)
            {
                DisplayError($"Column groups count ({columnGroups.Length}) must be divisible by value columns count ({valueColumns.Length})");
                return ExitCodes.InvalidArguments;
            }

            var columnsPerGroup = valueColumns.Length;
            var groupCount = columnGroups.Length / columnsPerGroup;
            var parsedGroups = new List<ColumnPairGroup>();

            for (int i = 0; i < groupCount; i++)
            {
                var groupColumns = columnGroups.Skip(i * columnsPerGroup).Take(columnsPerGroup).ToList();
                parsedGroups.Add(new ColumnPairGroup
                {
                    Columns = groupColumns,
                    IndexValue = (i + 1).ToString() // Sequential numbering
                });
            }

            // Validation with rich output
            var validationResults = new List<(string Name, bool IsValid, string? Error)>
            {
                ("Input file", ValidateInputFile(inputPath, out var inputError), inputError),
                ("Output directory", ValidateOutputPath(outputPath), ValidateOutputPath(outputPath) ? null : "Directory not found"),
                ("Base columns", true, $"{baseColumns.Length} column(s)"),
                ("Column groups", parsedGroups.Count > 0, parsedGroups.Count > 0 ? $"{parsedGroups.Count} group(s)" : "At least 1 group required"),
                ("Value columns", valueColumns.Length > 0, $"{valueColumns.Length} column(s)"),
                ("Index column", !string.IsNullOrWhiteSpace(indexColumn), !string.IsNullOrWhiteSpace(indexColumn) ? indexColumn : "Required"),
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
            var options = new UnpivotOption
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                BaseColumns = baseColumns.ToList(),
                ColumnGroups = parsedGroups,
                IndexColumnName = indexColumn,
                ValueColumnNames = valueColumns.ToList(),
                SkipEmptyRows = skipEmpty,
                HasHeader = hasHeader,
                IgnoreErrors = ignoreErrors
            };

            // Execute with progress display
            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Unpivoting data...", async ctx =>
                {
                    ctx.Status("Reading input file...");

                    var taskLogger = LoggerFactory.CreateLogger<UnpivotTask>();
                    var task = new UnpivotTask(taskLogger);
                    var taskContext = new TaskContext(options);

                    Logger.LogInformation(
                        "Unpivoting {Input} with {GroupCount} column groups",
                        Path.GetFileName(inputPath), parsedGroups.Count);

                    if (verbose)
                    {
                        Logger.LogInformation("  Base columns: {BaseColumns}", string.Join(", ", baseColumns));
                        Logger.LogInformation("  Value columns: {ValueColumns}", string.Join(", ", valueColumns));
                        Logger.LogInformation("  Index column: {IndexColumn}", indexColumn);
                    }

                    ctx.Status("Transforming wide to long format...");
                    var success = await task.ExecuteAsync(taskContext);

                    if (success)
                    {
                        DisplaySuccess($"Data unpivoted successfully: {outputPath}");

                        if (verbose)
                        {
                            var summaryText = $"""
                                [bold]Summary:[/]
                                • Input: {Markup.Escape(inputPath)}
                                • Output: {Markup.Escape(outputPath)}
                                • Base columns: {baseColumns.Length}
                                • Column groups: {parsedGroups.Count}
                                • Value columns: {string.Join(", ", valueColumns)}
                                • Index column: {indexColumn}
                                • Skip empty rows: {skipEmpty}
                                • Has header: {hasHeader}
                                """;

                            var panel = new Panel(new Markup(summaryText))
                            {
                                Header = new PanelHeader("Unpivot Complete", Justify.Center),
                                BorderStyle = new Style(Color.Green)
                            };
                            AnsiConsole.Write(panel);
                        }

                        return ExitCodes.Success;
                    }

                    DisplayError("Failed to unpivot data");
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
