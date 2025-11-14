using System.CommandLine;
using System.Globalization;
using FilePrepper.Tasks;
using FilePrepper.Tasks.DataTypeConvert;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

public class DataTypeConvertCommand : BaseCommand
{
    private readonly Option<string> _inputOption;
    private readonly Option<string> _outputOption;
    private readonly Option<string[]> _conversionsOption;
    private readonly Option<string> _cultureOption;

    public DataTypeConvertCommand(ILoggerFactory loggerFactory)
        : base("convert-type", "Convert data types of columns", loggerFactory)
    {
        _inputOption = new Option<string>("--input", new[] { "-i" }) { Description = "Input file path", Required = true };
        _outputOption = new Option<string>("--output", new[] { "-o" }) { Description = "Output file path", Required = true };
        _conversionsOption = new Option<string[]>("--conversions", new[] { "-c" }) { Description = "Type conversions in format column:type[:format] (e.g. Date:DateTime:yyyy-MM-dd)", CustomParser = result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries), Required = true };
        _cultureOption = new Option<string>("--culture") { Description = "Culture for parsing (e.g. en-US, ko-KR)", DefaultValueFactory = _ => "en-US" };

        Add(_inputOption);
        Add(_outputOption);
        Add(_conversionsOption);
        Add(_cultureOption);

        this.SetAction(async (parseResult) =>
        {
            return await ExecuteAsync(
                parseResult.GetValue(_inputOption)!,
                parseResult.GetValue(_outputOption)!,
                parseResult.GetValue(_conversionsOption)!,
                parseResult.GetValue(_cultureOption)!,
                parseResult.GetValue(CommonOptions.HasHeader),
                parseResult.GetValue(CommonOptions.IgnoreErrors),
                parseResult.GetValue(CommonOptions.Verbose));
        });
    }

    private async Task<int> ExecuteAsync(string inputPath, string outputPath, string[] conversionStrings,
        string cultureStr, bool hasHeader, bool ignoreErrors, bool verbose)
    {
        try
        {
            var conversions = new List<ColumnTypeConversion>();
            var culture = CultureInfo.GetCultureInfo(cultureStr);

            foreach (var convStr in conversionStrings)
            {
                var parts = convStr.Split(':');
                if (parts.Length < 2 || parts.Length > 3)
                {
                    DisplayError($"Invalid conversion format: {convStr}");
                    return ExitCodes.InvalidArguments;
                }

                if (!Enum.TryParse<DataType>(parts[1], true, out var dataType))
                {
                    DisplayError($"Invalid data type: {parts[1]}");
                    return ExitCodes.InvalidArguments;
                }

                conversions.Add(new ColumnTypeConversion
                {
                    ColumnName = parts[0],
                    TargetType = dataType,
                    DateTimeFormat = parts.Length == 3 ? parts[2] : null,
                    Culture = culture
                });
            }

            var options = new DataTypeConvertOption
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                Conversions = conversions,
                HasHeader = hasHeader,
                IgnoreErrors = ignoreErrors
            };

            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Converting data types...", async ctx =>
                {
                    var taskLogger = LoggerFactory.CreateLogger<DataTypeConvertTask>();
                    var task = new DataTypeConvertTask(taskLogger);
                    var taskContext = new TaskContext(options);

                    var success = await task.ExecuteAsync(taskContext);
                    if (success)
                    {
                        DisplaySuccess($"Data types converted: {outputPath}");
                        return ExitCodes.Success;
                    }

                    DisplayError("Failed to convert data types");
                    return ExitCodes.Error;
                });
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }
}
