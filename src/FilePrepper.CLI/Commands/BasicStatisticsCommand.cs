using System.CommandLine;
using FilePrepper.Tasks;
using FilePrepper.Tasks.BasicStatistics;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

public class BasicStatisticsCommand : BaseCommand
{
    private readonly Option<string> _inputOption;
    private readonly Option<string> _outputOption;
    private readonly Option<string[]> _columnsOption;
    private readonly Option<string[]> _statsOption;
    private readonly Option<string> _suffixOption;
    private readonly Option<string?> _defaultValueOption;

    public BasicStatisticsCommand(ILoggerFactory loggerFactory)
        : base("stats", "Calculate basic statistics on numeric columns", loggerFactory)
    {
        _inputOption = new Option<string>("--input", new[] { "-i" }) { Description = "Input file path", Required = true };
        _outputOption = new Option<string>("--output", new[] { "-o" }) { Description = "Output file path", Required = true };
        _columnsOption = new Option<string[]>("--columns", new[] { "-c" }) { Description = "Target columns", CustomParser = result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries), Required = true };
        _statsOption = new Option<string[]>("--stats", new[] { "-s" }) { Description = "Statistics to calculate (Mean/StandardDeviation/Min/Max/Median/Q1/Q3/ZScore/RobustZScore/PercentRank/MAD)", CustomParser = result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries), Required = true };
        _suffixOption = new Option<string>("--suffix") { Description = "Suffix for output column names", DefaultValueFactory = _ => "_stat" };
        _defaultValueOption = new Option<string?>("--default-value") { Description = "Default value for errors" };

        Add(_inputOption);
        Add(_outputOption);
        Add(_columnsOption);
        Add(_statsOption);
        Add(_suffixOption);
        Add(_defaultValueOption);

        this.SetAction(async (parseResult) =>
        {
            var inputPath = parseResult.GetValue(_inputOption)!;
            var outputPath = parseResult.GetValue(_outputOption)!;
            var columns = parseResult.GetValue(_columnsOption)!;
            var statsStrings = parseResult.GetValue(_statsOption)!;
            var suffix = parseResult.GetValue(_suffixOption)!;
            var defaultValue = parseResult.GetValue(_defaultValueOption);
            var hasHeader = parseResult.GetValue(CommonOptions.HasHeader);
            var ignoreErrors = parseResult.GetValue(CommonOptions.IgnoreErrors);
            var verbose = parseResult.GetValue(CommonOptions.Verbose);

            return await ExecuteAsync(inputPath, outputPath, columns, statsStrings, suffix, defaultValue,
                hasHeader, ignoreErrors, verbose);
        });
    }

    private async Task<int> ExecuteAsync(string inputPath, string outputPath, string[] columns, string[] statsStrings,
        string suffix, string? defaultValue, bool hasHeader, bool ignoreErrors, bool verbose)
    {
        try
        {
            var stats = new List<StatisticType>();
            var statErrors = new List<string>();

            foreach (var statStr in statsStrings)
            {
                if (!Enum.TryParse<StatisticType>(statStr, true, out var stat))
                {
                    statErrors.Add($"Invalid statistic: '{statStr}'");
                    continue;
                }
                stats.Add(stat);
            }

            var inputValid = ValidateInputFile(inputPath, out var inputError);
            var validationResults = new List<(string Name, bool IsValid, string? Error)>
            {
                ("Input file", inputValid, inputError),
                ("Columns", columns.Length > 0, null),
                ("Statistics", statErrors.Count == 0, statErrors.Any() ? string.Join("; ", statErrors) : null)
            };

            if (statErrors.Count == 0)
            {
                validationResults.Add(("Stats parsing", true, $"{stats.Count} statistic(s)"));
            }

            if (validationResults.Any(r => !r.IsValid))
            {
                DisplayError("Validation failed");
                return ExitCodes.InvalidArguments;
            }

            var options = new BasicStatisticsOption
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                TargetColumns = columns,
                Statistics = stats.ToArray(),
                Suffix = suffix,
                DefaultValue = defaultValue,
                HasHeader = hasHeader,
                IgnoreErrors = ignoreErrors
            };

            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Calculating statistics...", async ctx =>
                {
                    var taskLogger = LoggerFactory.CreateLogger<BasicStatisticsTask>();
                    var task = new BasicStatisticsTask(taskLogger);
                    var taskContext = new TaskContext(options);

                    var success = await task.ExecuteAsync(taskContext);
                    if (success)
                    {
                        DisplaySuccess($"Statistics calculated: {outputPath}");
                        return ExitCodes.Success;
                    }

                    DisplayError("Failed to calculate statistics");
                    return ExitCodes.Error;
                });
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }
}
