using System.CommandLine;

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

            // System.CommandLine 2.0: Parse and invoke
            try
            {
                var parseResult = rootCommand.Parse(args);
                return await parseResult.InvokeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in CLI");
                AnsiConsole.WriteException(ex,
                    ExceptionFormats.ShortenPaths | ExceptionFormats.ShortenTypes);
                return 1;
            }
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
        var rootCommand = new RootCommand("FilePrepper - ML Data Preprocessing Tool (CSV, TSV, JSON, XML, Excel)");

        // Add all commands via CommandFactory (single source of truth)
        foreach (var command in CommandFactory.CreateAllCommands(_loggerFactory))
        {
            rootCommand.Add(command);
        }

        // Add global options
        var quietOption = new Option<bool>("--quiet", new[] { "-q" })
        {
            Description = "Suppress non-error output",
            Recursive = true
        };

        var versionDetailOption = new Option<bool>("-v")
        {
            Description = "Show detailed version information",
            Recursive = true
        };

        rootCommand.Add(quietOption);
        rootCommand.Add(versionDetailOption);

        // Add handler for detailed version
        rootCommand.SetAction((parseResult) =>
        {
            bool showDetailedVersion = parseResult.GetValue(versionDetailOption);
            if (showDetailedVersion)
            {
                DisplayVersionInfo();
                return 0;
            }
            return 0;
        });

        return rootCommand;
    }
}
