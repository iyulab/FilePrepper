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
        _inputOption = new Option<string>("--input", new[] { "-i" }) { Description = "Input file path", Required = true };

        _outputOption = new Option<string>("--output", new[] { "-o" }) { Description = "Output file path", Required = true };

        _columnGroupsOption = new Option<string[]>("--column-groups", new[] { "-g" })
        {
            Description = "Column groups to unpivot (space-separated)",
            Required = true,
            AllowMultipleArgumentsPerToken = true
        };

        _valueColumnsOption = new Option<string[]>("--value-columns", new[] { "-vc" })
        {
            Description = "Names for value columns in output (space-separated)",
            Required = true,
            AllowMultipleArgumentsPerToken = true
        };

        // Optional options
        _baseColumnsOption = new Option<string[]>("--base-columns", new[] { "-b" })
        {
            Description = "Base columns to keep in every output row (space-separated)",
            DefaultValueFactory = _ => Array.Empty<string>(),
            AllowMultipleArgumentsPerToken = true
        };

        _indexColumnOption = new Option<string>("--index-column", new[] { "-idx" }) { Description = "Name for the index column in output", DefaultValueFactory = _ => "Index" };

        _skipEmptyOption = new Option<bool>("--skip-empty", new[] { "-se" }) { Description = "Skip rows where all value columns are empty", DefaultValueFactory = _ => true };

        // Add all options
        Add(_inputOption);
        Add(_outputOption);
        Add(_baseColumnsOption);
        Add(_columnGroupsOption);
        Add(_indexColumnOption);
        Add(_valueColumnsOption);
        Add(_skipEmptyOption);

        // Set the handler
        this.SetAction(async (parseResult) =>
        {
            var inputPath = parseResult.GetValue(_inputOption)!;
            var outputPath = parseResult.GetValue(_outputOption)!;
            var baseColumns = parseResult.GetValue(_baseColumnsOption) ?? Array.Empty<string>();
            var columnGroups = parseResult.GetValue(_columnGroupsOption)!;
            var indexColumn = parseResult.GetValue(_indexColumnOption)!;
            var valueColumns = parseResult.GetValue(_valueColumnsOption)!;
            var skipEmpty = parseResult.GetValue(_skipEmptyOption);
            var hasHeader = parseResult.GetValue(CommonOptions.HasHeader);
            var ignoreErrors = parseResult.GetValue(CommonOptions.IgnoreErrors);
            var verbose = parseResult.GetValue(CommonOptions.Verbose);

            return await ExecuteAsync(
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
