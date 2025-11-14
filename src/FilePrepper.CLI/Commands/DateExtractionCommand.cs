using System.CommandLine;
using System.Globalization;
using FilePrepper.Tasks;
using FilePrepper.Tasks.DateExtraction;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

public class DateExtractionCommand : BaseCommand
{
    private readonly Option<string> _inputOption;
    private readonly Option<string> _outputOption;
    private readonly Option<string[]> _extractionsOption;
    private readonly Option<string> _cultureOption;
    private readonly Option<bool> _appendOption;
    private readonly Option<string?> _templateOption;

    public DateExtractionCommand(ILoggerFactory loggerFactory)
        : base("extract-date", "Extract components from date columns", loggerFactory)
    {
        _inputOption = new Option<string>("--input", new[] { "-i" }) { Description = "Input file path", Required = true };
        _outputOption = new Option<string>("--output", new[] { "-o" }) { Description = "Output file path", Required = true };
        _extractionsOption = new Option<string[]>("--extractions", new[] { "-e" }) { Description = "Date extractions in format column:component1,component2[:format]", CustomParser = result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries), Required = true };
        _cultureOption = new Option<string>("--culture") { Description = "Culture for parsing dates", DefaultValueFactory = _ => "en-US" };
        _appendOption = new Option<bool>("--append-to-source") { Description = "Append to source file", DefaultValueFactory = _ => false };
        _templateOption = new Option<string?>("--output-column") { Description = "Output column template" };

        Add(_inputOption);
        Add(_outputOption);
        Add(_extractionsOption);
        Add(_cultureOption);
        Add(_appendOption);
        Add(_templateOption);

        this.SetAction(async (parseResult) =>
        {
            return await ExecuteAsync(
                parseResult.GetValue(_inputOption)!,
                parseResult.GetValue(_outputOption)!,
                parseResult.GetValue(_extractionsOption)!,
                parseResult.GetValue(_cultureOption)!,
                parseResult.GetValue(_appendOption),
                parseResult.GetValue(_templateOption),
                parseResult.GetValue(CommonOptions.HasHeader),
                parseResult.GetValue(CommonOptions.IgnoreErrors),
                parseResult.GetValue(CommonOptions.Verbose));
        });
    }

    private async Task<int> ExecuteAsync(string inputPath, string outputPath, string[] extractionStrings,
        string cultureStr, bool append, string? template, bool hasHeader, bool ignoreErrors, bool verbose)
    {
        try
        {
            var extractions = new List<DateColumnExtraction>();
            var culture = CultureInfo.GetCultureInfo(cultureStr);

            foreach (var extStr in extractionStrings)
            {
                var parts = extStr.Split(':');
                if (parts.Length < 2)
                {
                    DisplayError($"Invalid extraction format: {extStr}");
                    return ExitCodes.InvalidArguments;
                }

                var components = new List<DateComponent>();
                foreach (var comp in parts[1].Split(','))
                {
                    if (!Enum.TryParse<DateComponent>(comp, true, out var dateComp))
                    {
                        DisplayError($"Invalid date component: {comp}");
                        return ExitCodes.InvalidArguments;
                    }
                    components.Add(dateComp);
                }

                extractions.Add(new DateColumnExtraction
                {
                    SourceColumn = parts[0],
                    Components = components,
                    DateFormat = parts.Length > 2 ? parts[2] : null,
                    Culture = culture
                });
            }

            var options = new DateExtractionOption
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                Extractions = extractions,
                AppendToSource = append,
                OutputColumnTemplate = template,
                HasHeader = hasHeader,
                IgnoreErrors = ignoreErrors
            };

            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Extracting date components...", async ctx =>
                {
                    var taskLogger = LoggerFactory.CreateLogger<DateExtractionTask>();
                    var task = new DateExtractionTask(taskLogger);
                    var taskContext = new TaskContext(options);

                    var success = await task.ExecuteAsync(taskContext);
                    if (success)
                    {
                        DisplaySuccess($"Date extraction complete: {outputPath}");
                        return ExitCodes.Success;
                    }

                    DisplayError("Failed to extract date components");
                    return ExitCodes.Error;
                });
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }
}
