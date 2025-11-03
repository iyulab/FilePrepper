using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using FilePrepper.CLI.Commands;

namespace FilePrepper.CLI;

/// <summary>
/// Main entry point for FilePrepper CLI
/// Refactored to use System.CommandLine and Spectre.Console
/// </summary>
public class Program
{
    private static ILoggerFactory _loggerFactory = null!;
    private static ILogger<Program> _logger = null!;

    static async Task<int> Main(string[] args)
    {
        try
        {
            // Configure logging
            ConfigureLogging(LogLevel.Information);

            // Display banner
            DisplayBanner();

            // Build the root command
            var rootCommand = BuildRootCommand();

            // Create the parser with middleware
            var parser = new CommandLineBuilder(rootCommand)
                .UseVersionOption()
                .UseHelp()
                .UseEnvironmentVariableDirective()
                .UseParseDirective()
                .UseSuggestDirective()
                .RegisterWithDotnetSuggest()
                .UseTypoCorrections()
                .UseParseErrorReporting()
                .CancelOnProcessTermination()
                .UseExceptionHandler((exception, context) =>
                {
                    _logger.LogError(exception, "Unhandled exception in CLI");
                    AnsiConsole.WriteException(exception,
                        ExceptionFormats.ShortenPaths | ExceptionFormats.ShortenTypes);
                    context.ExitCode = 1;
                })
                .Build();

            // Parse and invoke
            return await parser.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            if (_logger != null)
            {
                _logger.LogError(ex, "Fatal error in CLI");
            }

            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private static void ConfigureLogging(LogLevel minLevel)
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddConsole()
                .SetMinimumLevel(minLevel);
        });

        _logger = _loggerFactory.CreateLogger<Program>();
    }

    private static void DisplayBanner()
    {
        var banner = new FigletText("FilePrepper")
            .LeftJustified()
            .Color(Color.Blue);

        AnsiConsole.Write(banner);
        AnsiConsole.MarkupLine("[dim]ML Data Preprocessing Tool - No Coding Required[/]");
        AnsiConsole.MarkupLine($"[dim]Version {GetVersion()} | CSV, TSV, JSON, XML, Excel Support[/]");
        AnsiConsole.WriteLine();
    }

    private static string GetVersion()
    {
        var version = typeof(Program).Assembly.GetName().Version;
        return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "0.2.3";
    }

    private static void DisplayVersionInfo()
    {
        var version = GetVersion();
        var assembly = typeof(Program).Assembly;
        var assemblyName = assembly.GetName();

        var panel = new Panel(
            new Markup($"""
                [bold blue]FilePrepper[/] [dim]v{version}[/]
                
                [yellow]ML Data Preprocessing Tool[/]
                [dim]Process data files without coding - normalize, transform, analyze, and convert formats[/]
                
                [bold]Supported Formats:[/]
                • CSV (Comma-Separated Values)
                • TSV (Tab-Separated Values)
                • JSON (JavaScript Object Notation)
                • XML (Extensible Markup Language)
                • Excel (XLSX format)
                
                [bold]Use Cases:[/]
                • Machine Learning data preprocessing
                • Feature engineering (lag features, encoding, scaling)
                • Data quality improvement (fill missing, drop duplicates)
                • Format conversion and data merging
                • Statistical analysis and aggregation
                
                [dim]Copyright (c) Iyulab Corporation 2024
                License: MIT
                Repository: https://github.com/iyulab/FilePrepper[/]
                """))
        {
            Header = new PanelHeader("FilePrepper - ML Data Preprocessing", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Blue)
        };

        AnsiConsole.Write(panel);
    }

    private static RootCommand BuildRootCommand()
    {
        var rootCommand = new RootCommand("FilePrepper - ML Data Preprocessing Tool (CSV, TSV, JSON, XML, Excel)")
        {
            Name = "fileprepper"
        };

        // Add commands (all 20 commands now migrated to System.CommandLine)
        rootCommand.AddCommand(new AddColumnsCommand(_loggerFactory));
        rootCommand.AddCommand(new AggregateCommand(_loggerFactory));
        rootCommand.AddCommand(new BasicStatisticsCommand(_loggerFactory));
        rootCommand.AddCommand(new ColumnInteractionCommand(_loggerFactory));
        rootCommand.AddCommand(new CreateLagFeaturesCommand(_loggerFactory));
        rootCommand.AddCommand(new DataSamplingCommand(_loggerFactory));
        rootCommand.AddCommand(new DataTypeConvertCommand(_loggerFactory));
        rootCommand.AddCommand(new DateExtractionCommand(_loggerFactory));
        rootCommand.AddCommand(new DropDuplicatesCommand(_loggerFactory));
        rootCommand.AddCommand(new FileFormatConvertCommand(_loggerFactory));
        rootCommand.AddCommand(new FillMissingValuesCommand(_loggerFactory));
        rootCommand.AddCommand(new FilterRowsCommand(_loggerFactory));
        rootCommand.AddCommand(new MergeCommand(_loggerFactory));
        rootCommand.AddCommand(new NormalizeDataCommand(_loggerFactory));
        rootCommand.AddCommand(new OneHotEncodingCommand(_loggerFactory));
        rootCommand.AddCommand(new RemoveColumnsCommand(_loggerFactory));
        rootCommand.AddCommand(new RenameColumnsCommand(_loggerFactory));
        rootCommand.AddCommand(new ReorderColumnsCommand(_loggerFactory));
        rootCommand.AddCommand(new ScaleDataCommand(_loggerFactory));
        rootCommand.AddCommand(new ValueReplaceCommand(_loggerFactory));

        // Add global options
        var quietOption = new Option<bool>(
            aliases: new[] { "--quiet", "-q" },
            description: "Suppress non-error output");

        var versionDetailOption = new Option<bool>(
            aliases: new[] { "-v" },
            description: "Show detailed version information");

        rootCommand.AddGlobalOption(quietOption);
        rootCommand.AddGlobalOption(versionDetailOption);

        // Add handler for detailed version
        rootCommand.SetHandler((bool showDetailedVersion) =>
        {
            if (showDetailedVersion)
            {
                DisplayVersionInfo();
            }
        }, versionDetailOption);

        return rootCommand;
    }
}
