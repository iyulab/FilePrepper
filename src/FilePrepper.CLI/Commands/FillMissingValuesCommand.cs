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
        _inputOption = new Option<string>(new[] { "--input", "-i" }, "Input file path") { IsRequired = true };
        _outputOption = new Option<string>(new[] { "--output", "-o" }, "Output file path") { IsRequired = true };
        _methodsOption = new Option<string[]>(
            aliases: new[] { "--methods", "-m" },
            description: "Fill methods in format column:method[:value] (e.g. Age:Mean or Score:FixedValue:0)",
            parseArgument: result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries)) { IsRequired = true };
        _appendOption = new Option<bool>("--append-to-source", () => false, "Append to source");
        _templateOption = new Option<string?>("--output-column", "Output column template");

        AddOption(_inputOption);
        AddOption(_outputOption);
        AddOption(_methodsOption);
        AddOption(_appendOption);
        AddOption(_templateOption);

        this.SetHandler(async (context) =>
        {
            context.ExitCode = await ExecuteAsync(
                context.ParseResult.GetValueForOption(_inputOption)!,
                context.ParseResult.GetValueForOption(_outputOption)!,
                context.ParseResult.GetValueForOption(_methodsOption)!,
                context.ParseResult.GetValueForOption(_appendOption),
                context.ParseResult.GetValueForOption(_templateOption),
                context.ParseResult.GetValueForOption(CommonOptions.HasHeader),
                context.ParseResult.GetValueForOption(CommonOptions.IgnoreErrors),
                context.ParseResult.GetValueForOption(CommonOptions.Verbose));
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
