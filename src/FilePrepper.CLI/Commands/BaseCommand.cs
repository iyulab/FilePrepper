using FilePrepper.Utils;
using System.CommandLine;
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
        Add(CommonOptions.HasHeader);
        Add(CommonOptions.IgnoreErrors);
        Add(CommonOptions.Verbose);
        Add(CommonOptions.Encoding);
        Add(CommonOptions.SkipRows);
    }

    /// <summary>Whether <c>--quiet</c> was given: only errors are written, to the console and to the log.</summary>
    public static bool Quiet { get; set; }

    /// <summary>What a command tells the status line while it works; with <c>--quiet</c> there is no status line and it tells nothing.</summary>
    protected sealed class StatusReport(StatusContext? context)
    {
        public void Status(string text)
        {
            if (context is not null)
                context.Status = text;
        }
    }

    /// <summary>
    /// Runs a command's work under a spinner with a line of text. <c>--quiet</c> promises to suppress non-error output, but the
    /// spinner's text is written whether or not the terminal is interactive, so all thirty-one commands said "Adding columns..."
    /// and the like under it; under <c>--quiet</c> the work runs with no spinner at all.
    /// </summary>
    protected static async Task<T> RunWithStatusAsync<T>(string text, Func<StatusReport, Task<T>> work)
    {
        if (Quiet)
            return await work(new StatusReport(null));

        return await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync(text, async context => await work(new StatusReport(context)));
    }

    protected void DisplaySuccess(string message)
    {
        if (Quiet)
            return;

        AnsiConsole.MarkupLine($"[green]✓[/] {Markup.Escape(message)}");
    }

    /// <summary>
    /// The checks a command made of its arguments, as a table of what each found; true when any failed, after saying which and why.
    /// Thirteen commands each carried a copy of this table, and the copy in <c>stats</c> had lost the table: it said "Validation
    /// failed" and not what was invalid, with the reason (an unknown statistic) already worked out and thrown away.
    /// </summary>
    protected bool RefuseInvalidArguments(IReadOnlyList<(string Name, bool IsValid, string? Error)> results, bool verbose)
    {
        var failed = results.Any(result => !result.IsValid);
        if (verbose || failed)
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Parameter")
                .AddColumn("Status");

            foreach (var (name, isValid, error) in results)
            {
                table.AddRow(
                    Markup.Escape(name),
                    isValid
                        ? $"[green]✓ {Markup.Escape(error ?? "Valid")}[/]"
                        : $"[red]✗ {Markup.Escape(error ?? "Invalid")}[/]");
            }

            AnsiConsole.Write(table);
        }

        if (failed)
            DisplayError("Validation failed. Please check your inputs.");

        return failed;
    }

    protected void DisplayError(string message)
    {
        AnsiConsole.MarkupLine($"[red]✗[/] {Markup.Escape(message)}");
    }

    protected void DisplayWarning(string message)
    {
        if (Quiet)
            return;

        AnsiConsole.MarkupLine($"[yellow]⚠[/] {Markup.Escape(message)}");
    }

    protected void DisplayInfo(string message)
    {
        if (Quiet)
            return;

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
        // A mistake of the caller (a bad value, a column that is not there, a file that cannot be read) is one line: the message
        // says what to change. Anything else is a defect and keeps its stack. Verbose output (-v) shows the stack of both.
        if (IsExpected(ex))
        {
            Logger.LogDebug(ex, "Error executing command");
        }
        else
        {
            Logger.LogError(ex, "Error executing command");
        }

        DisplayError($"Error: {ex.Message}");

        return ex switch
        {
            ArgumentException => ExitCodes.InvalidArguments,
            FileNotFoundException or IOException => ExitCodes.FileError,
            OperationCanceledException => ExitCodes.Cancelled,
            _ => ExitCodes.Error
        };
    }


    /// <summary>Whether the exception is a mistake of the caller rather than a defect in the tool.</summary>
    public static bool IsExpected(Exception ex) =>
        ex is ValidationException or ArgumentException or InvalidOperationException or IOException or OperationCanceledException or UnauthorizedAccessException;

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

        if (!DataFileFormats.IsReadable(inputPath))
        {
            errorMessage = $"Unsupported input format: {Path.GetExtension(inputPath).ToLowerInvariant()}. " +
                           $"Readable formats: {string.Join(", ", DataFileFormats.ReadableExtensions)} (XML is an output format only)";
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
            ".parquet" => "Parquet",
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
        table.AddRow("[cyan]XML[/]", ".xml", "Extensible Markup Language (output only)");
        table.AddRow("[cyan]Excel[/]", ".xlsx, .xls", "Microsoft Excel Spreadsheet");
        table.AddRow("[cyan]Parquet[/]", ".parquet", "Apache Parquet (input only; struct fields become dotted columns)");

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
    public static readonly Option<bool> HasHeader;
    public static readonly Option<bool> IgnoreErrors;
    public static readonly Option<bool> Verbose;
    public static readonly Option<string> Encoding;
    public static readonly Option<int> SkipRows;

    static CommonOptions()
    {
        HasHeader = new Option<bool>("--has-header", new[] { "--header" })
        {
            Description = "Whether input files have headers",
            DefaultValueFactory = _ => true
        };

        IgnoreErrors = new Option<bool>("--ignore-errors")
        {
            Description = "Whether to ignore errors during processing",
            DefaultValueFactory = _ => false
        };

        Verbose = new Option<bool>("--verbose", new[] { "-v" })
        {
            Description = "Enable verbose output",
            DefaultValueFactory = _ => false
        };

        Encoding = new Option<string>("--encoding")
        {
            Description = "File encoding (auto, utf-8, cp949, euc-kr)",
            DefaultValueFactory = _ => "auto"
        };

        SkipRows = new Option<int>("--skip-rows")
        {
            Description = "Number of rows to skip before header",
            DefaultValueFactory = _ => 0
        };
    }
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
