using System.CommandLine;
using FilePrepper.Tasks;
using FilePrepper.Tasks.ValueReplace;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

public class ValueReplaceCommand : BaseCommand
{
    private readonly Option<string> _inputOption;
    private readonly Option<string> _outputOption;
    private readonly Option<string[]> _replacementsOption;

    public ValueReplaceCommand(ILoggerFactory loggerFactory)
        : base("replace", "Replace values in specified columns", loggerFactory)
    {
        _inputOption = new Option<string>("--input", new[] { "-i" }) { Description = "Input file path", Required = true };
        _outputOption = new Option<string>("--output", new[] { "-o" }) { Description = "Output file path", Required = true };
        _replacementsOption = new Option<string[]>("--replacements", new[] { "-r" }) { Description = "Replacement rules in format column:oldValue=newValue[;oldValue2=newValue2]", CustomParser = result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries), Required = true };

        Add(_inputOption);
        Add(_outputOption);
        Add(_replacementsOption);

        this.SetAction(async (parseResult) =>
        {
            return await ExecuteAsync(
                parseResult.GetValue(_inputOption)!,
                parseResult.GetValue(_outputOption)!,
                parseResult.GetValue(_replacementsOption)!,
                parseResult.GetValue(CommonOptions.HasHeader),
                parseResult.GetValue(CommonOptions.IgnoreErrors),
                parseResult.GetValue(CommonOptions.Verbose));
        });
    }

    private async Task<int> ExecuteAsync(string inputPath, string outputPath, string[] replacementStrings,
        bool hasHeader, bool ignoreErrors, bool verbose)
    {
        try
        {
            var replaceMethods = new List<ColumnReplaceMethod>();

            foreach (var replaceStr in replacementStrings)
            {
                var parts = replaceStr.Split(':', 2);
                if (parts.Length != 2)
                {
                    DisplayError($"Invalid replacement format: {replaceStr}");
                    return ExitCodes.InvalidArguments;
                }

                var replacements = new Dictionary<string, string>();
                var rules = parts[1].Split(';');

                foreach (var rule in rules)
                {
                    var valueParts = rule.Split('=', 2);
                    if (valueParts.Length != 2)
                    {
                        DisplayError($"Invalid replacement rule: {rule}");
                        return ExitCodes.InvalidArguments;
                    }

                    replacements[valueParts[0]] = valueParts[1];
                }

                replaceMethods.Add(new ColumnReplaceMethod
                {
                    ColumnName = parts[0],
                    Replacements = replacements
                });
            }

            var options = new ValueReplaceOption
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                ReplaceMethods = replaceMethods,
                HasHeader = hasHeader,
                IgnoreErrors = ignoreErrors
            };

            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Replacing values...", async ctx =>
                {
                    var taskLogger = LoggerFactory.CreateLogger<ValueReplaceTask>();
                    var task = new ValueReplaceTask(taskLogger);
                    var taskContext = new TaskContext(options);

                    var success = await task.ExecuteAsync(taskContext);
                    if (success)
                    {
                        DisplaySuccess($"Values replaced: {outputPath}");
                        return ExitCodes.Success;
                    }

                    DisplayError("Failed to replace values");
                    return ExitCodes.Error;
                });
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }
}
