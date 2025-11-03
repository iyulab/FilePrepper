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
        _inputOption = new Option<string>(new[] { "--input", "-i" }, "Input file path") { IsRequired = true };
        _outputOption = new Option<string>(new[] { "--output", "-o" }, "Output file path") { IsRequired = true };
        _columnsOption = new Option<string[]>(
            aliases: new[] { "--columns", "-c" },
            description: "Target columns",
            parseArgument: result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries)) { IsRequired = true };
        _statsOption = new Option<string[]>(
            aliases: new[] { "--stats", "-s" },
            description: "Statistics to calculate (Mean/StandardDeviation/Min/Max/Median/Q1/Q3/ZScore/RobustZScore/PercentRank/MAD)",
            parseArgument: result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries)) { IsRequired = true };
        _suffixOption = new Option<string>("--suffix", () => "_stat", "Suffix for output column names");
        _defaultValueOption = new Option<string?>("--default-value", "Default value for errors");

        AddOption(_inputOption);
        AddOption(_outputOption);
        AddOption(_columnsOption);
        AddOption(_statsOption);
        AddOption(_suffixOption);
        AddOption(_defaultValueOption);

        this.SetHandler(async (context) =>
        {
            var inputPath = context.ParseResult.GetValueForOption(_inputOption)!;
            var outputPath = context.ParseResult.GetValueForOption(_outputOption)!;
            var columns = context.ParseResult.GetValueForOption(_columnsOption)!;
            var statsStrings = context.ParseResult.GetValueForOption(_statsOption)!;
            var suffix = context.ParseResult.GetValueForOption(_suffixOption)!;
            var defaultValue = context.ParseResult.GetValueForOption(_defaultValueOption);
            var hasHeader = context.ParseResult.GetValueForOption(CommonOptions.HasHeader);
            var ignoreErrors = context.ParseResult.GetValueForOption(CommonOptions.IgnoreErrors);
            var verbose = context.ParseResult.GetValueForOption(CommonOptions.Verbose);

            context.ExitCode = await ExecuteAsync(inputPath, outputPath, columns, statsStrings, suffix, defaultValue,
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
