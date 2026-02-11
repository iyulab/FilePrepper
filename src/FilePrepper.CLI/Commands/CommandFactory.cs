using System.CommandLine;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FilePrepper.CLI.Commands;

/// <summary>
/// Factory for creating all FilePrepper commands
/// </summary>
public static class CommandFactory
{
    /// <summary>
    /// Creates all available commands
    /// </summary>
    public static IEnumerable<Command> CreateAllCommands(ILoggerFactory loggerFactory)
    {
        return new Command[]
        {
            // Data Transformation
            new AddColumnsCommand(loggerFactory),
            new RemoveColumnsCommand(loggerFactory),
            new RenameColumnsCommand(loggerFactory),
            new ReorderColumnsCommand(loggerFactory),
            new UnpivotCommand(loggerFactory),

            // Data Quality
            new CSVCleanerCommand(loggerFactory),
            new DropDuplicatesCommand(loggerFactory),
            new FillMissingValuesCommand(loggerFactory),
            new RemoveConstantsCommand(loggerFactory),

            // Data Processing
            new AggregateCommand(loggerFactory),
            new ConditionalCommand(loggerFactory),
            new ExpressionCommand(loggerFactory),
            new FilterRowsCommand(loggerFactory),
            new MergeCommand(loggerFactory),
            new MergeAsOfCommand(loggerFactory),
            new StringCommand(loggerFactory),
            new ValueReplaceCommand(loggerFactory),

            // Data Conversion
            new DataTypeConvertCommand(loggerFactory),
            new FileFormatConvertCommand(loggerFactory),

            // Data Analysis
            new BasicStatisticsCommand(loggerFactory),

            // Feature Engineering
            new ColumnInteractionCommand(loggerFactory),
            new CreateLagFeaturesCommand(loggerFactory),
            new DateExtractionCommand(loggerFactory),
            new DateTimeCommand(loggerFactory),
            new OneHotEncodingCommand(loggerFactory),
            new WindowCommand(loggerFactory),

            // Data Scaling & Normalization
            new NormalizeDataCommand(loggerFactory),
            new ScaleDataCommand(loggerFactory),

            // Sampling
            new DataSamplingCommand(loggerFactory),
        };
    }

    /// <summary>
    /// Gets command metadata for documentation
    /// </summary>
    public static IEnumerable<(string Name, string Description, string Category)> GetCommandMetadata()
    {
        return new[]
        {
            // Data Transformation
            ("add-columns", "Add new columns with specified values", "Data Transformation"),
            ("remove-columns", "Remove specified columns", "Data Transformation"),
            ("rename-columns", "Rename columns using mapping", "Data Transformation"),
            ("reorder-columns", "Reorder columns in specified order", "Data Transformation"),
            ("unpivot", "Transform wide format data to long format", "Data Transformation"),

            // Data Quality
            ("csv-cleaner", "Clean and fix CSV formatting issues", "Data Quality"),
            ("drop-duplicates", "Remove duplicate rows", "Data Quality"),
            ("fill-missing", "Fill missing values using various methods", "Data Quality"),
            ("remove-constants", "Remove constant or near-constant columns", "Data Quality"),

            // Data Processing
            ("aggregate", "Aggregate data based on grouping columns", "Data Processing"),
            ("conditional", "Apply conditional logic to create or update columns", "Data Processing"),
            ("expression", "Evaluate expressions to create computed columns", "Data Processing"),
            ("filter-rows", "Filter rows based on conditions", "Data Processing"),
            ("merge", "Merge multiple CSV files", "Data Processing"),
            ("merge-asof", "Merge files using as-of (nearest match) join", "Data Processing"),
            ("string", "Apply string transformations to columns", "Data Processing"),
            ("replace", "Replace values in columns", "Data Processing"),

            // Data Conversion
            ("convert-type", "Convert data types of columns", "Data Conversion"),
            ("convert-format", "Convert file format (CSV/TSV/JSON/XML/Excel)", "Data Conversion"),

            // Data Analysis
            ("stats", "Calculate basic statistics on numeric columns", "Data Analysis"),

            // Feature Engineering
            ("column-interaction", "Perform operations between columns", "Feature Engineering"),
            ("create-lag-features", "Create lag features from time series data", "Feature Engineering"),
            ("extract-date", "Extract components from date columns", "Feature Engineering"),
            ("datetime", "Parse and transform datetime columns", "Feature Engineering"),
            ("one-hot-encoding", "Perform one-hot encoding on categorical columns", "Feature Engineering"),
            ("window", "Calculate window/rolling aggregations", "Feature Engineering"),

            // Data Scaling & Normalization
            ("normalize", "Normalize numeric columns", "Data Scaling"),
            ("scale", "Scale numeric columns", "Data Scaling"),

            // Sampling
            ("data-sampling", "Sample data using various methods", "Sampling"),
        };
    }

    /// <summary>
    /// Creates a categorized help display
    /// </summary>
    public static void DisplayCategorizedCommands()
    {
        var metadata = GetCommandMetadata()
            .GroupBy(m => m.Category)
            .OrderBy(g => g.Key);

        foreach (var category in metadata)
        {
            AnsiConsole.MarkupLine($"\n[bold yellow]{category.Key}:[/]");
            
            var table = new Table()
                .Border(TableBorder.None)
                .HideHeaders()
                .AddColumn(new TableColumn("Command").Width(25))
                .AddColumn(new TableColumn("Description"));

            foreach (var (name, description, _) in category.OrderBy(m => m.Name))
            {
                table.AddRow($"[cyan]{name}[/]", description);
            }

            AnsiConsole.Write(table);
        }
    }
}
