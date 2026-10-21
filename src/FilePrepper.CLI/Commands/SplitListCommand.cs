using System.CommandLine;
using FilePrepper.Tasks;
using FilePrepper.Tasks.SplitList;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

public class SplitListCommand : BaseCommand
{
    private readonly Option<string> _inputOption;
    private readonly Option<string> _outputOption;
    private readonly Option<string[]> _columnsOption;

    public SplitListCommand(ILoggerFactory loggerFactory)
        : base("split-list", "Split list columns of one length (a JSON list per cell) into one column per position: name.0, name.1, …", loggerFactory)
    {
        _inputOption = new Option<string>("--input", new[] { "-i" }) { Description = "Input file path", Required = true };
        _outputOption = new Option<string>("--output", new[] { "-o" }) { Description = "Output file path", Required = true };
        _columnsOption = new Option<string[]>("--columns", new[] { "-c" })
        {
            Description = "List columns to split (comma-separated); every list of a column must have the same length",
            CustomParser = result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            Required = true
        };

        Add(_inputOption);
        Add(_outputOption);
        Add(_columnsOption);

        this.SetAction(async (parseResult) =>
        {
            var encoding = parseResult.GetValue(CommonOptions.Encoding) ?? "auto";
            var skipRows = parseResult.GetValue(CommonOptions.SkipRows);

            return await ExecuteAsync(
                parseResult.GetValue(_inputOption)!,
                parseResult.GetValue(_outputOption)!,
                parseResult.GetValue(_columnsOption)!,
                parseResult.GetValue(CommonOptions.HasHeader),
                parseResult.GetValue(CommonOptions.IgnoreErrors),
                encoding,
                skipRows);
        });
    }

    private async Task<int> ExecuteAsync(string inputPath, string outputPath, string[] columns,
        bool hasHeader, bool ignoreErrors, string encoding, int skipRows)
    {
        try
        {
            var options = new SplitListOption
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                Columns = columns.ToList(),
                HasHeader = hasHeader,
                IgnoreErrors = ignoreErrors,
                Encoding = encoding,
                SkipRows = skipRows
            };

            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Splitting list columns...", async ctx =>
                {
                    var task = new SplitListTask(LoggerFactory.CreateLogger<SplitListTask>());
                    var success = await task.ExecuteAsync(new TaskContext(options));
                    if (success)
                    {
                        DisplaySuccess($"List columns split: {outputPath}");
                        return ExitCodes.Success;
                    }

                    DisplayError("Failed to split list columns");
                    return ExitCodes.Error;
                });
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }
}
