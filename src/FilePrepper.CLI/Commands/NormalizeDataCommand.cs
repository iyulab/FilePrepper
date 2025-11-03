using System.CommandLine;
using FilePrepper.Tasks;
using FilePrepper.Tasks.NormalizeData;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

public class NormalizeDataCommand : BaseCommand
{
    private readonly Option<string> _inputOption;
    private readonly Option<string> _outputOption;
    private readonly Option<string[]> _columnsOption;
    private readonly Option<string> _methodOption;
    private readonly Option<double> _minOption;
    private readonly Option<double> _maxOption;

    public NormalizeDataCommand(ILoggerFactory loggerFactory)
        : base("normalize", "Normalize numeric columns", loggerFactory)
    {
        _inputOption = new Option<string>(new[] { "--input", "-i" }, "Input file path") { IsRequired = true };
        _outputOption = new Option<string>(new[] { "--output", "-o" }, "Output file path") { IsRequired = true };
        _columnsOption = new Option<string[]>(
            aliases: new[] { "--columns", "-c" },
            description: "Columns to normalize",
            parseArgument: result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries)) { IsRequired = true };
        _methodOption = new Option<string>(new[] { "--method", "-m" }, "Normalization method (MinMax/ZScore)") { IsRequired = true };
        _minOption = new Option<double>("--min", () => 0.0, "Min value for MinMax scaling");
        _maxOption = new Option<double>("--max", () => 1.0, "Max value for MinMax scaling");

        AddOption(_inputOption);
        AddOption(_outputOption);
        AddOption(_columnsOption);
        AddOption(_methodOption);
        AddOption(_minOption);
        AddOption(_maxOption);

        this.SetHandler(async (context) =>
        {
            context.ExitCode = await ExecuteAsync(
                context.ParseResult.GetValueForOption(_inputOption)!,
                context.ParseResult.GetValueForOption(_outputOption)!,
                context.ParseResult.GetValueForOption(_columnsOption)!,
                context.ParseResult.GetValueForOption(_methodOption)!,
                context.ParseResult.GetValueForOption(_minOption),
                context.ParseResult.GetValueForOption(_maxOption),
                context.ParseResult.GetValueForOption(CommonOptions.HasHeader),
                context.ParseResult.GetValueForOption(CommonOptions.IgnoreErrors),
                context.ParseResult.GetValueForOption(CommonOptions.Verbose));
        });
    }

    private async Task<int> ExecuteAsync(string inputPath, string outputPath, string[] columns, string methodStr,
        double min, double max, bool hasHeader, bool ignoreErrors, bool verbose)
    {
        try
        {
            if (!Enum.TryParse<NormalizationMethod>(methodStr, true, out var method))
            {
                DisplayError($"Invalid normalization method: {methodStr}");
                return ExitCodes.InvalidArguments;
            }

            if (method == NormalizationMethod.MinMax && min >= max)
            {
                DisplayError("Min value must be less than max value");
                return ExitCodes.InvalidArguments;
            }

            var options = new NormalizeDataOption
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                TargetColumns = columns,
                Method = method,
                MinValue = min,
                MaxValue = max,
                HasHeader = hasHeader,
                IgnoreErrors = ignoreErrors
            };

            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Normalizing data...", async ctx =>
                {
                    var taskLogger = LoggerFactory.CreateLogger<NormalizeDataTask>();
                    var task = new NormalizeDataTask(taskLogger);
                    var taskContext = new TaskContext(options);

                    var success = await task.ExecuteAsync(taskContext);
                    if (success)
                    {
                        DisplaySuccess($"Data normalized: {outputPath}");
                        return ExitCodes.Success;
                    }

                    DisplayError("Failed to normalize data");
                    return ExitCodes.Error;
                });
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }
}
