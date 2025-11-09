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
        _inputOption = new Option<string>(
            aliases: new[] { "--input", "-i" },
            description: "Input file path")
        { IsRequired = true };

        _outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            description: "Output file path")
        { IsRequired = true };

        _typeOption = new Option<string>(
            aliases: new[] { "--type", "-t" },
            description: "Window type: resample (time-based) or rolling (row-based)")
        { IsRequired = true };

        _methodOption = new Option<string>(
            aliases: new[] { "--method", "-m" },
            getDefaultValue: () => "mean",
            description: "Aggregation method: mean, min, max, sum, count, std");

        _columnsOption = new Option<string[]>(
            aliases: new[] { "--columns", "-c" },
            description: "Target columns to aggregate (comma-separated)",
            parseArgument: result => result.Tokens[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
        { IsRequired = true };

        // Resample-specific options
        _timeColumnOption = new Option<string?>(
            aliases: new[] { "--time-column", "-tc" },
            description: "Time column for resample (required for resample type)");

        _windowOption = new Option<string?>(
            aliases: new[] { "--window", "-w" },
            description: "Window size for resample: e.g., '5T' (5 minutes), '1H' (1 hour), '1D' (1 day)");

        // Rolling-specific options
        _windowSizeOption = new Option<int>(
            aliases: new[] { "--window-size", "-ws" },
            getDefaultValue: () => 3,
            description: "Window size in rows for rolling aggregation");

        _suffixOption = new Option<string>(
            aliases: new[] { "--suffix", "-s" },
            getDefaultValue: () => "_rolling",
            description: "Suffix for rolling aggregation output columns");

        // Add all options
        AddOption(_inputOption);
        AddOption(_outputOption);
        AddOption(_typeOption);
        AddOption(_methodOption);
        AddOption(_columnsOption);
        AddOption(_timeColumnOption);
        AddOption(_windowOption);
        AddOption(_windowSizeOption);
        AddOption(_suffixOption);

        // Set the handler
        this.SetHandler(async (context) =>
        {
            var inputPath = context.ParseResult.GetValueForOption(_inputOption)!;
            var outputPath = context.ParseResult.GetValueForOption(_outputOption)!;
            var type = context.ParseResult.GetValueForOption(_typeOption)!;
            var method = context.ParseResult.GetValueForOption(_methodOption)!;
            var columns = context.ParseResult.GetValueForOption(_columnsOption) ?? Array.Empty<string>();
            var timeColumn = context.ParseResult.GetValueForOption(_timeColumnOption);
            var window = context.ParseResult.GetValueForOption(_windowOption);
            var windowSize = context.ParseResult.GetValueForOption(_windowSizeOption);
            var suffix = context.ParseResult.GetValueForOption(_suffixOption)!;
            var hasHeader = context.ParseResult.GetValueForOption(CommonOptions.HasHeader);
            var ignoreErrors = context.ParseResult.GetValueForOption(CommonOptions.IgnoreErrors);
            var verbose = context.ParseResult.GetValueForOption(CommonOptions.Verbose);

            context.ExitCode = await ExecuteAsync(
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
