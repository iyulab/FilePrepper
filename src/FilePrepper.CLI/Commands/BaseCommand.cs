using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

/// <summary>
/// Base class for all FilePrepper CLI commands using System.CommandLine
/// </summary>
public abstract class BaseCommand : Command
{
    protected readonly ILoggerFactory LoggerFactory;
    protected readonly ILogger Logger;

    protected BaseCommand(
        string name,
        string description,
        ILoggerFactory loggerFactory) : base(name, description)
    {
        LoggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        Logger = LoggerFactory.CreateLogger(GetType());

        // Add common options
        AddOption(CommonOptions.HasHeader);
        AddOption(CommonOptions.IgnoreErrors);
        AddOption(CommonOptions.Verbose);
    }

    protected void DisplaySuccess(string message)
    {
        AnsiConsole.MarkupLine($"[green]✓[/] {Markup.Escape(message)}");
    }

    protected void DisplayError(string message)
    {
        AnsiConsole.MarkupLine($"[red]✗[/] {Markup.Escape(message)}");
    }

    protected void DisplayWarning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]⚠[/] {Markup.Escape(message)}");
    }

    protected void DisplayInfo(string message)
    {
        AnsiConsole.MarkupLine($"[blue]ℹ[/] {Markup.Escape(message)}");
    }

    protected async Task<int> ExecuteWithProgressAsync(
        string taskDescription,
        Func<ProgressContext, Task<int>> action)
    {
        return await AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask(taskDescription);
                return await action(ctx);
            });
    }

    protected int HandleError(Exception ex)
    {
        Logger.LogError(ex, "Error executing command");
        
        DisplayError($"Error: {ex.Message}");
        
        return ex switch
        {
            ArgumentException => ExitCodes.InvalidArguments,
            FileNotFoundException or IOException => ExitCodes.FileError,
            OperationCanceledException => ExitCodes.Cancelled,
            _ => ExitCodes.Error
        };
    }


    /// <summary>
    /// Validates input file and checks supported format
    /// </summary>
    protected bool ValidateInputFile(string inputPath, out string? errorMessage)
    {
        if (!File.Exists(inputPath))
        {
            errorMessage = "Input file not found";
            return false;
        }

        var extension = Path.GetExtension(inputPath).ToLowerInvariant();
        var supportedFormats = new[] { ".csv", ".tsv", ".json", ".xml", ".xlsx", ".xls" };

        if (!supportedFormats.Contains(extension))
        {
            errorMessage = $"Unsupported file format: {extension}. Supported formats: CSV, TSV, JSON, XML, Excel (XLSX/XLS)";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Gets a user-friendly format name from file extension
    /// </summary>
    protected string GetFormatName(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".csv" => "CSV",
            ".tsv" => "TSV",
            ".json" => "JSON",
            ".xml" => "XML",
            ".xlsx" or ".xls" => "Excel",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Displays supported file formats in a formatted table
    /// </summary>
    protected void DisplaySupportedFormats()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("Format").Centered())
            .AddColumn(new TableColumn("Extension").Centered())
            .AddColumn(new TableColumn("Description").LeftAligned());

        table.AddRow("[cyan]CSV[/]", ".csv", "Comma-Separated Values");
        table.AddRow("[cyan]TSV[/]", ".tsv", "Tab-Separated Values");
        table.AddRow("[cyan]JSON[/]", ".json", "JavaScript Object Notation");
        table.AddRow("[cyan]XML[/]", ".xml", "Extensible Markup Language");
        table.AddRow("[cyan]Excel[/]", ".xlsx, .xls", "Microsoft Excel Spreadsheet");

        var panel = new Panel(table)
        {
            Header = new PanelHeader("Supported File Formats", Justify.Center),
            BorderStyle = new Style(Color.Green)
        };

        AnsiConsole.Write(panel);
    }
}

/// <summary>
/// Common options shared across all commands
/// </summary>
public static class CommonOptions
{
    public static readonly Option<bool> HasHeader = new(
        aliases: new[] { "--has-header", "--header" },
        getDefaultValue: () => true,
        description: "Whether input files have headers");

    public static readonly Option<bool> IgnoreErrors = new(
        aliases: new[] { "--ignore-errors" },
        getDefaultValue: () => false,
        description: "Whether to ignore errors during processing");

    public static readonly Option<bool> Verbose = new(
        aliases: new[] { "--verbose", "-v" },
        getDefaultValue: () => false,
        description: "Enable verbose output");
}

/// <summary>
/// Exit codes for CLI commands
/// </summary>
public static class ExitCodes
{
    public const int Success = 0;
    public const int Error = 1;
    public const int InvalidArguments = 2;
    public const int FileError = 3;
    public const int Cancelled = 4;
}
