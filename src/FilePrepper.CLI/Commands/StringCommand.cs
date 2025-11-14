using System.CommandLine;
using FilePrepper.Tasks;
using FilePrepper.Tasks.StringOps;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

/// <summary>
/// Command for string operations (substring, concat, replace, trim, upper, lower)
/// </summary>
public class StringCommand : BaseCommand
{
    private readonly Option<string> _inputOption;
    private readonly Option<string> _outputOption;
    private readonly Option<string> _modeOption;
    private readonly Option<string?> _columnOption;
    private readonly Option<string?> _outputColumnOption;

    // Substring options
    private readonly Option<int> _startIndexOption;
    private readonly Option<int?> _lengthOption;

    // Concat options
    private readonly Option<string?> _columnsOption;
    private readonly Option<string> _separatorOption;

    // Replace options
    private readonly Option<string?> _oldValueOption;
    private readonly Option<string?> _newValueOption;

    // Trim options
    private readonly Option<string> _trimModeOption;

    public StringCommand(ILoggerFactory loggerFactory)
        : base("string", "String operations (substring, concat, replace, trim, upper, lower)", loggerFactory)
    {
        // Required options
        _inputOption = new Option<string>("--input", new[] { "-i" }) { Description = "Input file path", Required = true };

        _outputOption = new Option<string>("--output", new[] { "-o" }) { Description = "Output file path", Required = true };

        _modeOption = new Option<string>("--mode", new[] { "-m" }) { Description = "Operation mode: substring, concat, replace, trim, upper, lower", DefaultValueFactory = _ => "trim" };

        _columnOption = new Option<string?>("--column", new[] { "-c" }) { Description = "Column to operate on" };

        _outputColumnOption = new Option<string?>("--output-column", new[] { "-oc" }) { Description = "Output column name (default: same as input or auto-generated)" };

        // Substring options
        _startIndexOption = new Option<int>("--start-index", new[] { "-s" }) { Description = "Start index for substring (0-based)", DefaultValueFactory = _ => 0 };

        _lengthOption = new Option<int?>("--length", new[] { "-l" }) { Description = "Length for substring (default: to end)" };

        // Concat options
        _columnsOption = new Option<string?>("--columns", new[] { "-cols" }) { Description = "Columns to concatenate (comma-separated)" };

        _separatorOption = new Option<string>("--separator", new[] { "-sep" }) { Description = "Separator for concat", DefaultValueFactory = _ => "" };

        // Replace options
        _oldValueOption = new Option<string?>("--old-value", new[] { "-old" }) { Description = "Value to replace" };

        _newValueOption = new Option<string?>("--new-value", new[] { "-new" }) { Description = "Replacement value" };

        // Trim options
        _trimModeOption = new Option<string>("--trim-mode", new[] { "-tm" }) { Description = "Trim mode: both, start, end", DefaultValueFactory = _ => "both" };

        // Add all options
        Add(_inputOption);
        Add(_outputOption);
        Add(_modeOption);
        Add(_columnOption);
        Add(_outputColumnOption);
        Add(_startIndexOption);
        Add(_lengthOption);
        Add(_columnsOption);
        Add(_separatorOption);
        Add(_oldValueOption);
        Add(_newValueOption);
        Add(_trimModeOption);

        // Set the handler
        this.SetAction(async (parseResult) =>
        {
            var inputPath = parseResult.GetValue(_inputOption)!;
            var outputPath = parseResult.GetValue(_outputOption)!;
            var mode = parseResult.GetValue(_modeOption)!;
            var column = parseResult.GetValue(_columnOption);
            var outputColumn = parseResult.GetValue(_outputColumnOption);
            var startIndex = parseResult.GetValue(_startIndexOption);
            var length = parseResult.GetValue(_lengthOption);
            var columns = parseResult.GetValue(_columnsOption);
            var separator = parseResult.GetValue(_separatorOption)!;
            var oldValue = parseResult.GetValue(_oldValueOption);
            var newValue = parseResult.GetValue(_newValueOption);
            var trimMode = parseResult.GetValue(_trimModeOption)!;
            var hasHeader = parseResult.GetValue(CommonOptions.HasHeader);
            var ignoreErrors = parseResult.GetValue(CommonOptions.IgnoreErrors);
            var verbose = parseResult.GetValue(CommonOptions.Verbose);

            return await ExecuteAsync(
                inputPath, outputPath, mode, column, outputColumn, startIndex, length,
                columns, separator, oldValue, newValue, trimMode,
                hasHeader, ignoreErrors, verbose);
        });
    }

    private async Task<int> ExecuteAsync(
        string inputPath, string outputPath, string mode, string? column, string? outputColumn,
        int startIndex, int? length, string? columns, string separator,
        string? oldValue, string? newValue, string trimMode,
        bool hasHeader, bool ignoreErrors, bool verbose)
    {
        try
        {
            // Parse mode
            var stringMode = mode.ToLower() switch
            {
                "substring" => StringMode.Substring,
                "concat" => StringMode.Concat,
                "replace" => StringMode.Replace,
                "trim" => StringMode.Trim,
                "upper" => StringMode.Upper,
                "lower" => StringMode.Lower,
                _ => throw new ArgumentException($"Invalid mode: {mode}. Valid modes: substring, concat, replace, trim, upper, lower")
            };

            // Parse trim mode
            var trimModeEnum = trimMode.ToLower() switch
            {
                "both" => TrimMode.Both,
                "start" => TrimMode.Start,
                "end" => TrimMode.End,
                _ => TrimMode.Both
            };

            // Validation
            var validationResults = new List<(string Name, bool IsValid, string? Error)>
            {
                ("Input file", ValidateInputFile(inputPath, out var inputError), inputError),
                ("Output directory", ValidateOutputPath(outputPath), ValidateOutputPath(outputPath) ? null : "Directory not found"),
                ("Mode", true, mode),
            };

            if (stringMode == StringMode.Substring || stringMode == StringMode.Replace ||
                stringMode == StringMode.Trim || stringMode == StringMode.Upper || stringMode == StringMode.Lower)
            {
                validationResults.Add(("Column", !string.IsNullOrWhiteSpace(column), column ?? "Required"));
            }

            if (stringMode == StringMode.Concat)
            {
                validationResults.Add(("Columns", !string.IsNullOrWhiteSpace(columns), columns ?? "Required for concat"));
                validationResults.Add(("Output column", !string.IsNullOrWhiteSpace(outputColumn), outputColumn ?? "Required for concat"));
            }

            if (stringMode == StringMode.Replace)
            {
                validationResults.Add(("Old value", !string.IsNullOrWhiteSpace(oldValue), oldValue ?? "Required for replace"));
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
            var options = new StringOption
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                Mode = stringMode,
                Column = column ?? "",
                OutputColumn = outputColumn,
                StartIndex = startIndex,
                Length = length,
                Columns = columns,
                Separator = separator,
                OldValue = oldValue,
                NewValue = newValue,
                TrimMode = trimModeEnum,
                HasHeader = hasHeader,
                IgnoreErrors = ignoreErrors
            };

            // Execute with progress display
            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Processing string operation...", async ctx =>
                {
                    ctx.Status("Reading input file...");

                    var taskLogger = LoggerFactory.CreateLogger<StringTask>();
                    var task = new StringTask(taskLogger);
                    var taskContext = new TaskContext(options);

                    Logger.LogInformation("Processing string operation: {Mode}", mode);

                    if (verbose)
                    {
                        Logger.LogInformation("  Column: {Column}", column);
                        if (stringMode == StringMode.Substring)
                        {
                            Logger.LogInformation("  Start index: {Start}, Length: {Length}",
                                startIndex, length?.ToString() ?? "to end");
                        }
                        else if (stringMode == StringMode.Concat)
                        {
                            Logger.LogInformation("  Columns: {Columns}, Separator: '{Sep}'", columns, separator);
                        }
                        else if (stringMode == StringMode.Replace)
                        {
                            Logger.LogInformation("  Replace: '{Old}' → '{New}'", oldValue, newValue ?? "");
                        }
                    }

                    ctx.Status($"Applying {mode} operation...");
                    var success = await task.ExecuteAsync(taskContext);

                    if (success)
                    {
                        DisplaySuccess($"String operation completed successfully: {outputPath}");

                        if (verbose)
                        {
                            var summaryText = $"""
                                [bold]Summary:[/]
                                • Input: {Markup.Escape(inputPath)}
                                • Output: {Markup.Escape(outputPath)}
                                • Mode: {mode}
                                • Has header: {hasHeader}
                                """;

                            var panel = new Panel(new Markup(summaryText))
                            {
                                Header = new PanelHeader("String Operation Complete", Justify.Center),
                                BorderStyle = new Style(Color.Green)
                            };
                            AnsiConsole.Write(panel);
                        }

                        return ExitCodes.Success;
                    }

                    DisplayError("Failed to process string operation");
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
