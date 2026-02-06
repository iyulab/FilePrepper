using System.CommandLine;
using FilePrepper.Tasks;
using FilePrepper.Tasks.RemoveConstants;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

public class RemoveConstantsCommand : BaseCommand
{
    private readonly Option<string> _inputOption;
    private readonly Option<string> _outputOption;
    private readonly Option<double> _thresholdOption;
    private readonly Option<bool> _reportOnlyOption;

    public RemoveConstantsCommand(ILoggerFactory loggerFactory)
        : base("remove-constants", "Remove constant or near-constant columns from the input file", loggerFactory)
    {
        _inputOption = new Option<string>("--input", new[] { "-i" }) { Description = "Input file path", Required = true };
        _outputOption = new Option<string>("--output", new[] { "-o" }) { Description = "Output file path", Required = true };
        _thresholdOption = new Option<double>("--threshold", new[] { "-t" })
        {
            Description = "Unique ratio threshold (0.0 = exact constants only, 0.01 = below 1% unique ratio)",
            DefaultValueFactory = _ => 0.0
        };
        _reportOnlyOption = new Option<bool>("--report-only")
        {
            Description = "Only report constant columns without removing them",
            DefaultValueFactory = _ => false
        };

        Add(_inputOption);
        Add(_outputOption);
        Add(_thresholdOption);
        Add(_reportOnlyOption);

        this.SetAction(async (parseResult) =>
        {
            var encoding = parseResult.GetValue(CommonOptions.Encoding) ?? "auto";
            var skipRows = parseResult.GetValue(CommonOptions.SkipRows);

            return await ExecuteAsync(
                parseResult.GetValue(_inputOption)!,
                parseResult.GetValue(_outputOption)!,
                parseResult.GetValue(_thresholdOption),
                parseResult.GetValue(_reportOnlyOption),
                parseResult.GetValue(CommonOptions.HasHeader),
                parseResult.GetValue(CommonOptions.IgnoreErrors),
                parseResult.GetValue(CommonOptions.Verbose),
                encoding,
                skipRows);
        });
    }

    private async Task<int> ExecuteAsync(string inputPath, string outputPath, double threshold,
        bool reportOnly, bool hasHeader, bool ignoreErrors, bool verbose, string encoding, int skipRows)
    {
        try
        {
            var options = new RemoveConstantsOption
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                UniqueRatioThreshold = threshold,
                ReportOnly = reportOnly,
                HasHeader = hasHeader,
                IgnoreErrors = ignoreErrors,
                Encoding = encoding,
                SkipRows = skipRows
            };

            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Analyzing columns...", async ctx =>
                {
                    var taskLogger = LoggerFactory.CreateLogger<RemoveConstantsTask>();
                    var task = new RemoveConstantsTask(taskLogger);
                    var taskContext = new TaskContext(options);

                    var success = await task.ExecuteAsync(taskContext);
                    if (success)
                    {
                        if (reportOnly)
                        {
                            DisplaySuccess("Column analysis complete (report-only mode)");
                        }
                        else
                        {
                            DisplaySuccess($"Constant columns removed: {outputPath}");
                        }
                        return ExitCodes.Success;
                    }

                    DisplayError("Failed to remove constant columns");
                    return ExitCodes.Error;
                });
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }
}
