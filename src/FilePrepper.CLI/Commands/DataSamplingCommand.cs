using System.CommandLine;
using FilePrepper.Tasks;
using FilePrepper.Tasks.DataSampling;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

public class DataSamplingCommand : BaseCommand
{
    private readonly Option<string> _inputOption;
    private readonly Option<string> _outputOption;
    private readonly Option<string> _methodOption;
    private readonly Option<double> _sizeOption;
    private readonly Option<int?> _seedOption;
    private readonly Option<string?> _stratifyOption;
    private readonly Option<int?> _intervalOption;

    public DataSamplingCommand(ILoggerFactory loggerFactory)
        : base("data-sampling", "Sample data from the input file", loggerFactory)
    {
        _inputOption = new Option<string>("--input", new[] { "-i" }) { Description = "Input file path", Required = true };
        _outputOption = new Option<string>("--output", new[] { "-o" }) { Description = "Output file path", Required = true };
        _methodOption = new Option<string>("--method", new[] { "-m" }) { Description = "Sampling method (Random/Systematic/Stratified)", Required = true };
        _sizeOption = new Option<double>("--size", new[] { "-s" }) { Description = "Sample size (ratio 0-1 or absolute count)", Required = true };
        _seedOption = new Option<int?>("--seed", "Random seed for reproducibility");
        _stratifyOption = new Option<string?>("--stratify", "Column for stratified sampling");
        _intervalOption = new Option<int?>("--interval", "Interval for systematic sampling");

        Add(_inputOption);
        Add(_outputOption);
        Add(_methodOption);
        Add(_sizeOption);
        Add(_seedOption);
        Add(_stratifyOption);
        Add(_intervalOption);

        this.SetAction(async (parseResult) =>
        {
            return await ExecuteAsync(
                parseResult.GetValue(_inputOption)!,
                parseResult.GetValue(_outputOption)!,
                parseResult.GetValue(_methodOption)!,
                parseResult.GetValue(_sizeOption),
                parseResult.GetValue(_seedOption),
                parseResult.GetValue(_stratifyOption),
                parseResult.GetValue(_intervalOption),
                parseResult.GetValue(CommonOptions.HasHeader),
                parseResult.GetValue(CommonOptions.IgnoreErrors),
                parseResult.GetValue(CommonOptions.Verbose));
        });
    }

    private async Task<int> ExecuteAsync(string inputPath, string outputPath, string methodStr, double sampleSize,
        int? seed, string? stratifyColumn, int? interval, bool hasHeader, bool ignoreErrors, bool verbose)
    {
        try
        {
            if (!Enum.TryParse<SamplingMethod>(methodStr, true, out var method))
            {
                DisplayError($"Invalid sampling method: {methodStr}");
                return ExitCodes.InvalidArguments;
            }

            var options = new DataSamplingOption
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                Method = method,
                SampleSize = sampleSize,
                Seed = seed,
                StratifyColumn = stratifyColumn,
                SystematicInterval = interval,
                HasHeader = hasHeader,
                IgnoreErrors = ignoreErrors
            };

            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Sampling data...", async ctx =>
                {
                    var taskLogger = LoggerFactory.CreateLogger<DataSamplingTask>();
                    var task = new DataSamplingTask(taskLogger);
                    var taskContext = new TaskContext(options);

                    var success = await task.ExecuteAsync(taskContext);
                    if (success)
                    {
                        DisplaySuccess($"Data sampled: {outputPath}");
                        return ExitCodes.Success;
                    }

                    DisplayError("Failed to sample data");
                    return ExitCodes.Error;
                });
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }
}
