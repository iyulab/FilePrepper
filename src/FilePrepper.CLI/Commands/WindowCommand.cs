using System.CommandLine;
using FilePrepper.Tasks;
using FilePrepper.Tasks.WindowOps;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

/// <summary>
/// Command for window operations (resample and rolling aggregations)
/// </summary>
public class WindowCommand : BaseCommand
{
    private readonly Option<string> _inputOption;
    private readonly Option<string> _outputOption;
    private readonly Option<string> _typeOption;
    private readonly Option<string> _methodOption;
    private readonly Option<string[]> _columnsOption;
    private readonly Option<string?> _timeColumnOption;
    private readonly Option<string?> _windowOption;
    private readonly Option<int> _windowSizeOption;
    private readonly Option<string> _suffixOption;

    public WindowCommand(ILoggerFactory loggerFactory)
        : base("window", "Apply window operations (resample or rolling aggregations)", loggerFactory)
    {
        // Required options
        _inputOption = new Option<string>("--input", new[] { "-i" }) { Description = "Input file path", Required = true };

        _outputOption = new Option<string>("--output", new[] { "-o" }) { Description = "Output file path", Required = true };

        _typeOption = new Option<string>("--type", new[] { "-t" }) { Description = "Window type: resample (time-based) or rolling (row-based)", Required = true };

        _methodOption = new Option<string>("--method", new[] { "-m" }) { Description = "Aggregation method: mean, min, max, sum, count, std", DefaultValueFactory = _ => "mean" };

        _columnsOption = new Option<string[]>("--columns", new[] { "-c" }) { Description = "Target columns to aggregate (comma-separated)", CustomParser = result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries), Required = true };

        // Resample-specific options
        _timeColumnOption = new Option<string?>("--time-column", new[] { "-tc" }) { Description = "Time column for resample (required for resample type)" };

        _windowOption = new Option<string?>("--window", new[] { "-w" }) { Description = "Window size for resample: e.g., '5T' (5 minutes), '1H' (1 hour), '1D' (1 day)" };

        // Rolling-specific options
        _windowSizeOption = new Option<int>("--window-size", new[] { "-ws" }) { Description = "Window size in rows for rolling aggregation", DefaultValueFactory = _ => 3 };

        _suffixOption = new Option<string>("--suffix", new[] { "-s" }) { Description = "Suffix for rolling aggregation output columns", DefaultValueFactory = _ => "_rolling" };

        // Add all options
        Add(_inputOption);
        Add(_outputOption);
        Add(_typeOption);
        Add(_methodOption);
        Add(_columnsOption);
        Add(_timeColumnOption);
        Add(_windowOption);
        Add(_windowSizeOption);
        Add(_suffixOption);

        // Set the handler
        this.SetAction(async (parseResult) =>
        {
            var inputPath = parseResult.GetValue(_inputOption)!;
            var outputPath = parseResult.GetValue(_outputOption)!;
            var type = parseResult.GetValue(_typeOption)!;
            var method = parseResult.GetValue(_methodOption)!;
            var columns = parseResult.GetValue(_columnsOption) ?? Array.Empty<string>();
            var timeColumn = parseResult.GetValue(_timeColumnOption);
            var window = parseResult.GetValue(_windowOption);
            var windowSize = parseResult.GetValue(_windowSizeOption);
            var suffix = parseResult.GetValue(_suffixOption)!;
            var hasHeader = parseResult.GetValue(CommonOptions.HasHeader);
            var ignoreErrors = parseResult.GetValue(CommonOptions.IgnoreErrors);
            var verbose = parseResult.GetValue(CommonOptions.Verbose);

            return await ExecuteAsync(
                inputPath, outputPath, type, method, columns, timeColumn, window, windowSize, suffix,
                hasHeader, ignoreErrors, verbose);
        });
    }

    private async Task<int> ExecuteAsync(
        string inputPath, string outputPath, string type, string method, string[] columns,
        string? timeColumn, string? window, int windowSize, string suffix,
        bool hasHeader, bool ignoreErrors, bool verbose)
    {
        try
        {
            Logger.LogInformation("Processing Window operation on {Column}", string.Join(", ", columns));

            // Parse window type
            var windowType = type.ToLowerInvariant() switch
            {
                "resample" => WindowType.Resample,
                "rolling" => WindowType.Rolling,
                _ => throw new ArgumentException($"Invalid window type: {type}. Use 'resample' or 'rolling'.")
            };

            // Parse aggregation method
            var aggregationMethod = method.ToLowerInvariant() switch
            {
                "mean" => AggregationMethod.Mean,
                "min" => AggregationMethod.Min,
                "max" => AggregationMethod.Max,
                "sum" => AggregationMethod.Sum,
                "count" => AggregationMethod.Count,
                "std" => AggregationMethod.Std,
                _ => throw new ArgumentException($"Invalid aggregation method: {method}")
            };

            // Validate type-specific requirements
            if (windowType == WindowType.Resample)
            {
                if (string.IsNullOrEmpty(timeColumn))
                {
                    AnsiConsole.MarkupLine("[red]Error: --time-column is required for resample operation[/]");
                    return 1;
                }

                if (string.IsNullOrEmpty(window))
                {
                    AnsiConsole.MarkupLine("[red]Error: --window is required for resample operation[/]");
                    return 1;
                }
            }

            // Create options
            var options = new WindowOption
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                Type = windowType,
                Method = aggregationMethod,
                TargetColumns = columns,
                TimeColumn = timeColumn,
                Window = window,
                WindowSize = windowSize,
                OutputSuffix = suffix,
                HasHeader = hasHeader,
                IgnoreErrors = ignoreErrors
            };

            // Validate options
            var validationErrors = options.Validate();
            if (validationErrors.Any())
            {
                foreach (var error in validationErrors)
                {
                    AnsiConsole.MarkupLine($"[red]Validation error: {error}[/]");
                }
                return 1;
            }

            // Create and execute task
            var taskLogger = LoggerFactory.CreateLogger<WindowTask>();
            var task = new WindowTask(taskLogger);
            var taskContext = new TaskContext(options);

            bool success = await task.ExecuteAsync(taskContext);

            if (success)
            {
                AnsiConsole.MarkupLine("[green]✓ Window operation completed successfully[/]");
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine("[red]✗ Window operation failed[/]");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error executing window command");
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
}
