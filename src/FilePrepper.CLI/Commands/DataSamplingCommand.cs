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
        _inputOption = new Option<string>(new[] { "--input", "-i" }, "Input file path") { IsRequired = true };
        _outputOption = new Option<string>(new[] { "--output", "-o" }, "Output file path") { IsRequired = true };
        _methodOption = new Option<string>(new[] { "--method", "-m" }, "Sampling method (Random/Systematic/Stratified)") { IsRequired = true };
        _sizeOption = new Option<double>(new[] { "--size", "-s" }, "Sample size (ratio 0-1 or absolute count)") { IsRequired = true };
        _seedOption = new Option<int?>("--seed", "Random seed for reproducibility");
        _stratifyOption = new Option<string?>("--stratify", "Column for stratified sampling");
        _intervalOption = new Option<int?>("--interval", "Interval for systematic sampling");

        AddOption(_inputOption);
        AddOption(_outputOption);
        AddOption(_methodOption);
        AddOption(_sizeOption);
        AddOption(_seedOption);
        AddOption(_stratifyOption);
        AddOption(_intervalOption);

        this.SetHandler(async (context) =>
        {
            context.ExitCode = await ExecuteAsync(
                context.ParseResult.GetValueForOption(_inputOption)!,
                context.ParseResult.GetValueForOption(_outputOption)!,
                context.ParseResult.GetValueForOption(_methodOption)!,
                context.ParseResult.GetValueForOption(_sizeOption),
                context.ParseResult.GetValueForOption(_seedOption),
                context.ParseResult.GetValueForOption(_stratifyOption),
                context.ParseResult.GetValueForOption(_intervalOption),
                context.ParseResult.GetValueForOption(CommonOptions.HasHeader),
                context.ParseResult.GetValueForOption(CommonOptions.IgnoreErrors),
                context.ParseResult.GetValueForOption(CommonOptions.Verbose));
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
