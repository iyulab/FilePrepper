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
        _inputOption = new Option<string>("--input", new[] { "-i" }) { Description = "Input file path", Required = true };
        _outputOption = new Option<string>("--output", new[] { "-o" }) { Description = "Output file path", Required = true };
        _sourceOption = new Option<string[]>("--source", new[] { "-s" }) { Description = "Source columns", CustomParser = result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries), Required = true };
        _typeOption = new Option<string>("--type", new[] { "-t" }) { Description = "Operation type (Add/Subtract/Multiply/Divide/Concat/Custom)", Required = true };
        _columnOption = new Option<string>("--column", new[] { "-c" }) { Description = "Output column name", Required = true };
        _expressionOption = new Option<string?>("--expression", new[] { "-e" }) { Description = "Custom expression (use $1, $2, etc.)" };
        _defaultValueOption = new Option<string?>("--default-value", "Default value for errors");

        Add(_inputOption);
        Add(_outputOption);
        Add(_sourceOption);
        Add(_typeOption);
        Add(_columnOption);
        Add(_expressionOption);
        Add(_defaultValueOption);

        this.SetAction(async (parseResult) =>
        {
            var inputPath = parseResult.GetValue(_inputOption)!;
            var outputPath = parseResult.GetValue(_outputOption)!;
            var source = parseResult.GetValue(_sourceOption)!;
            var type = parseResult.GetValue(_typeOption)!;
            var column = parseResult.GetValue(_columnOption)!;
            var expression = parseResult.GetValue(_expressionOption);
            var defaultValue = parseResult.GetValue(_defaultValueOption);
            var hasHeader = parseResult.GetValue(CommonOptions.HasHeader);
            var ignoreErrors = parseResult.GetValue(CommonOptions.IgnoreErrors);
            var verbose = parseResult.GetValue(CommonOptions.Verbose);

            return await ExecuteAsync(inputPath, outputPath, source, type, column, expression,
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
