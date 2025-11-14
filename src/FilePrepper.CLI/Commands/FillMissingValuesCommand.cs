using System.CommandLine;
using FilePrepper.Tasks;
using FilePrepper.Tasks.FillMissingValues;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

public class FillMissingValuesCommand : BaseCommand
{
    private readonly Option<string> _inputOption;
    private readonly Option<string> _outputOption;
    private readonly Option<string[]> _methodsOption;
    private readonly Option<bool> _appendOption;
    private readonly Option<string?> _templateOption;

    public FillMissingValuesCommand(ILoggerFactory loggerFactory)
        : base("fill-missing", "Fill missing values in columns", loggerFactory)
    {
        _inputOption = new Option<string>("--input", new[] { "-i" }) { Description = "Input file path", Required = true };
        _outputOption = new Option<string>("--output", new[] { "-o" }) { Description = "Output file path", Required = true };
        _methodsOption = new Option<string[]>("--methods", new[] { "-m" }) { Description = "Fill methods in format column:method[:value] (e.g. Age:Mean or Score:FixedValue:0)", CustomParser = result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries), Required = true };
        _appendOption = new Option<bool>("--append-to-source") { Description = "Append to source", DefaultValueFactory = _ => false };
        _templateOption = new Option<string?>("--output-column") { Description = "Output column template" };

        Add(_inputOption);
        Add(_outputOption);
        Add(_methodsOption);
        Add(_appendOption);
        Add(_templateOption);

        this.SetAction(async (parseResult) =>
        {
            return await ExecuteAsync(
                parseResult.GetValue(_inputOption)!,
                parseResult.GetValue(_outputOption)!,
                parseResult.GetValue(_methodsOption)!,
                parseResult.GetValue(_appendOption),
                parseResult.GetValue(_templateOption),
                parseResult.GetValue(CommonOptions.HasHeader),
                parseResult.GetValue(CommonOptions.IgnoreErrors),
                parseResult.GetValue(CommonOptions.Verbose));
        });
    }

    private async Task<int> ExecuteAsync(string inputPath, string outputPath, string[] methodStrings,
        bool append, string? template, bool hasHeader, bool ignoreErrors, bool verbose)
    {
        try
        {
            var methods = new List<ColumnFillMethod>();

            foreach (var methodStr in methodStrings)
            {
                var parts = methodStr.Split(':');
                if (parts.Length < 2 || parts.Length > 3)
                {
                    DisplayError($"Invalid fill method format: {methodStr}");
                    return ExitCodes.InvalidArguments;
                }

                if (!Enum.TryParse<FillMethod>(parts[1], true, out var fillMethod))
                {
                    DisplayError($"Invalid fill method: {parts[1]}");
                    return ExitCodes.InvalidArguments;
                }

                if (fillMethod == FillMethod.FixedValue && parts.Length != 3)
                {
                    DisplayError($"Fixed value required for FixedValue method: {methodStr}");
                    return ExitCodes.InvalidArguments;
                }

                methods.Add(new ColumnFillMethod
                {
                    ColumnName = parts[0],
                    Method = fillMethod,
                    FixedValue = parts.Length > 2 ? parts[2] : null
                });
            }

            var options = new FillMissingValuesOption
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                FillMethods = methods,
                AppendToSource = append,
                OutputColumnTemplate = template,
                HasHeader = hasHeader,
                IgnoreErrors = ignoreErrors
            };

            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Filling missing values...", async ctx =>
                {
                    var taskLogger = LoggerFactory.CreateLogger<FillMissingValuesTask>();
                    var task = new FillMissingValuesTask(taskLogger);
                    var taskContext = new TaskContext(options);

                    var success = await task.ExecuteAsync(taskContext);
                    if (success)
                    {
                        DisplaySuccess($"Missing values filled: {outputPath}");
                        return ExitCodes.Success;
                    }

                    DisplayError("Failed to fill missing values");
                    return ExitCodes.Error;
                });
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }
}
