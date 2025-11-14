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
        _inputOption = new Option<string>("--input", new[] { "-i" }) { Description = "Input file path", Required = true };
        _outputOption = new Option<string>("--output", new[] { "-o" }) { Description = "Output file path", Required = true };
        _columnsOption = new Option<string[]>("--columns", new[] { "-c" }) { Description = "Columns to normalize", CustomParser = result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries), Required = true };
        _methodOption = new Option<string>("--method", new[] { "-m" }) { Description = "Normalization method (MinMax/ZScore)", Required = true };
        _minOption = new Option<double>("--min") { Description = "Min value for MinMax scaling", DefaultValueFactory = _ => 0.0 };
        _maxOption = new Option<double>("--max") { Description = "Max value for MinMax scaling", DefaultValueFactory = _ => 1.0 };

        Add(_inputOption);
        Add(_outputOption);
        Add(_columnsOption);
        Add(_methodOption);
        Add(_minOption);
        Add(_maxOption);

        this.SetAction(async (parseResult) =>
        {
            return await ExecuteAsync(
                parseResult.GetValue(_inputOption)!,
                parseResult.GetValue(_outputOption)!,
                parseResult.GetValue(_columnsOption)!,
                parseResult.GetValue(_methodOption)!,
                parseResult.GetValue(_minOption),
                parseResult.GetValue(_maxOption),
                parseResult.GetValue(CommonOptions.HasHeader),
                parseResult.GetValue(CommonOptions.IgnoreErrors),
                parseResult.GetValue(CommonOptions.Verbose));
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
