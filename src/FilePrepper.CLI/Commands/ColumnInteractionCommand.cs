using System.CommandLine;
using FilePrepper.Tasks;
using FilePrepper.Tasks.ColumnInteraction;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

public class ColumnInteractionCommand : BaseCommand
{
    private readonly Option<string> _inputOption;
    private readonly Option<string> _outputOption;
    private readonly Option<string[]> _sourceOption;
    private readonly Option<string> _typeOption;
    private readonly Option<string> _columnOption;
    private readonly Option<string?> _expressionOption;
    private readonly Option<string?> _defaultValueOption;

    public ColumnInteractionCommand(ILoggerFactory loggerFactory)
        : base("column-interaction", "Perform operations between columns", loggerFactory)
    {
        _inputOption = new Option<string>(new[] { "--input", "-i" }, "Input file path") { IsRequired = true };
        _outputOption = new Option<string>(new[] { "--output", "-o" }, "Output file path") { IsRequired = true };
        _sourceOption = new Option<string[]>(
            aliases: new[] { "--source", "-s" },
            description: "Source columns",
            parseArgument: result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries)) { IsRequired = true };
        _typeOption = new Option<string>(new[] { "--type", "-t" }, "Operation type (Add/Subtract/Multiply/Divide/Concat/Custom)") { IsRequired = true };
        _columnOption = new Option<string>(new[] { "--column", "-c" }, "Output column name") { IsRequired = true };
        _expressionOption = new Option<string?>(new[] { "--expression", "-e" }, "Custom expression (use $1, $2, etc.)");
        _defaultValueOption = new Option<string?>("--default-value", "Default value for errors");

        AddOption(_inputOption);
        AddOption(_outputOption);
        AddOption(_sourceOption);
        AddOption(_typeOption);
        AddOption(_columnOption);
        AddOption(_expressionOption);
        AddOption(_defaultValueOption);

        this.SetHandler(async (context) =>
        {
            var inputPath = context.ParseResult.GetValueForOption(_inputOption)!;
            var outputPath = context.ParseResult.GetValueForOption(_outputOption)!;
            var source = context.ParseResult.GetValueForOption(_sourceOption)!;
            var type = context.ParseResult.GetValueForOption(_typeOption)!;
            var column = context.ParseResult.GetValueForOption(_columnOption)!;
            var expression = context.ParseResult.GetValueForOption(_expressionOption);
            var defaultValue = context.ParseResult.GetValueForOption(_defaultValueOption);
            var hasHeader = context.ParseResult.GetValueForOption(CommonOptions.HasHeader);
            var ignoreErrors = context.ParseResult.GetValueForOption(CommonOptions.IgnoreErrors);
            var verbose = context.ParseResult.GetValueForOption(CommonOptions.Verbose);

            context.ExitCode = await ExecuteAsync(inputPath, outputPath, source, type, column, expression,
                defaultValue, hasHeader, ignoreErrors, verbose);
        });
    }

    private async Task<int> ExecuteAsync(string inputPath, string outputPath, string[] sourceColumns, string typeStr,
        string outputColumn, string? customExpression, string? defaultValue, bool hasHeader, bool ignoreErrors, bool verbose)
    {
        try
        {
            if (!Enum.TryParse<OperationType>(typeStr, true, out var operationType))
            {
                DisplayError($"Invalid operation type: {typeStr}");
                return ExitCodes.InvalidArguments;
            }

            if (operationType == OperationType.Custom && string.IsNullOrWhiteSpace(customExpression))
            {
                DisplayError("Custom expression required for Custom operation type");
                return ExitCodes.InvalidArguments;
            }

            var options = new ColumnInteractionOption
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                SourceColumns = sourceColumns,
                Operation = operationType,
                OutputColumn = outputColumn,
                CustomExpression = customExpression,
                DefaultValue = defaultValue,
                HasHeader = hasHeader,
                IgnoreErrors = ignoreErrors
            };

            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Performing column interaction...", async ctx =>
                {
                    var taskLogger = LoggerFactory.CreateLogger<ColumnInteractionTask>();
                    var task = new ColumnInteractionTask(taskLogger);
                    var taskContext = new TaskContext(options);

                    var success = await task.ExecuteAsync(taskContext);
                    if (success)
                    {
                        DisplaySuccess($"Column interaction complete: {outputPath}");
                        return ExitCodes.Success;
                    }

                    DisplayError("Failed to perform column interaction");
                    return ExitCodes.Error;
                });
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }
}
