using System.CommandLine;
using FilePrepper.Tasks;
using FilePrepper.Tasks.ScaleData;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

public class ScaleDataCommand : BaseCommand
{
    private readonly Option<string> _inputOption;
    private readonly Option<string> _outputOption;
    private readonly Option<string[]> _scalingOption;

    public ScaleDataCommand(ILoggerFactory loggerFactory)
        : base("scale", "Scale numeric columns using specified methods", loggerFactory)
    {
        _inputOption = new Option<string>("--input", new[] { "-i" }) { Description = "Input file path", Required = true };
        _outputOption = new Option<string>("--output", new[] { "-o" }) { Description = "Output file path", Required = true };
        _scalingOption = new Option<string[]>("--scaling", new[] { "-s" }) { Description = "Scaling methods in format column:method (e.g. Price:MinMax,Score:Standardization)", CustomParser = result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries), Required = true };

        Add(_inputOption);
        Add(_outputOption);
        Add(_scalingOption);

        this.SetAction(async (parseResult) =>
        {
            return await ExecuteAsync(
                parseResult.GetValue(_inputOption)!,
                parseResult.GetValue(_outputOption)!,
                parseResult.GetValue(_scalingOption)!,
                parseResult.GetValue(CommonOptions.HasHeader),
                parseResult.GetValue(CommonOptions.IgnoreErrors),
                parseResult.GetValue(CommonOptions.Verbose));
        });
    }

    private async Task<int> ExecuteAsync(string inputPath, string outputPath, string[] scalingStrings,
        bool hasHeader, bool ignoreErrors, bool verbose)
    {
        try
        {
            var scaleColumns = new List<ScaleColumnOption>();

            foreach (var scaleStr in scalingStrings)
            {
                var parts = scaleStr.Split(':');
                if (parts.Length != 2)
                {
                    DisplayError($"Invalid scaling format: {scaleStr}");
                    return ExitCodes.InvalidArguments;
                }

                if (!Enum.TryParse<ScaleMethod>(parts[1], true, out var method))
                {
                    DisplayError($"Invalid scale method: {parts[1]}");
                    return ExitCodes.InvalidArguments;
                }

                scaleColumns.Add(new ScaleColumnOption
                {
                    ColumnName = parts[0],
                    Method = method
                });
            }

            var options = new ScaleDataOption
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                ScaleColumns = scaleColumns,
                HasHeader = hasHeader,
                IgnoreErrors = ignoreErrors
            };

            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Scaling data...", async ctx =>
                {
                    var taskLogger = LoggerFactory.CreateLogger<ScaleDataTask>();
                    var task = new ScaleDataTask(taskLogger);
                    var taskContext = new TaskContext(options);

                    var success = await task.ExecuteAsync(taskContext);
                    if (success)
                    {
                        DisplaySuccess($"Data scaled: {outputPath}");
                        return ExitCodes.Success;
                    }

                    DisplayError("Failed to scale data");
                    return ExitCodes.Error;
                });
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }
}
