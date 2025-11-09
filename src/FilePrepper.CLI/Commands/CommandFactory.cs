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
            // Time Series & Features
            new CreateLagFeaturesCommand(loggerFactory),

            // Data Transformation
            new UnpivotCommand(loggerFactory),

            // TODO: Add remaining commands as they are migrated
            // Data Transformation
            // new AddColumnsCommand(loggerFactory),
            // new RemoveColumnsCommand(loggerFactory),
            // new RenameColumnsCommand(loggerFactory),
            // new ReorderColumnsCommand(loggerFactory),
            
            // Data Quality
            // new FillMissingValuesCommand(loggerFactory),
            // new DropDuplicatesCommand(loggerFactory),
            
            // Data Processing
            // new AggregateCommand(loggerFactory),
            // new FilterRowsCommand(loggerFactory),
            // new MergeCommand(loggerFactory),
            
            // Data Conversion
            // new DataTypeConvertCommand(loggerFactory),
            // new FileFormatConvertCommand(loggerFactory),
            
            // Data Analysis
            // new BasicStatisticsCommand(loggerFactory),
            
            // Feature Engineering
            // new DateExtractionCommand(loggerFactory),
            // new OneHotEncodingCommand(loggerFactory),
            // new ColumnInteractionCommand(loggerFactory),
            
            // Data Scaling & Normalization
            // new ScaleDataCommand(loggerFactory),
            // new NormalizeDataCommand(loggerFactory),
            
            // Sampling & Replacement
            // new DataSamplingCommand(loggerFactory),
            // new ValueReplaceCommand(loggerFactory),
        };
    }

    /// <summary>
    /// Gets command metadata for documentation
    /// </summary>
    public static IEnumerable<(string Name, string Description, string Category)> GetCommandMetadata()
    {
        return new[]
        {
            ("create-lag-features", "Create lag features from time series data", "Time Series"),
            ("unpivot", "Transform wide format data to long format", "Data Transformation"),
            ("add-columns", "Add new columns with specified values", "Data Transformation"),
            ("aggregate", "Aggregate data based on grouping columns", "Data Processing"),
            ("stats", "Calculate basic statistics on numeric columns", "Data Analysis"),
            ("column-interaction", "Perform operations between columns", "Feature Engineering"),
            ("data-sampling", "Sample data using various methods", "Sampling"),
            ("convert-type", "Convert data types of columns", "Data Conversion"),
            ("extract-date", "Extract components from date columns", "Feature Engineering"),
            ("drop-duplicates", "Remove duplicate rows", "Data Quality"),
            ("convert-format", "Convert file format (CSV/TSV/JSON/XML/Excel)", "Data Conversion"),
            ("fill-missing", "Fill missing values using various methods", "Data Quality"),
            ("filter-rows", "Filter rows based on conditions", "Data Processing"),
            ("merge", "Merge multiple CSV files", "Data Processing"),
            ("normalize", "Normalize numeric columns", "Data Scaling"),
            ("one-hot-encoding", "Perform one-hot encoding on categorical columns", "Feature Engineering"),
            ("remove-columns", "Remove specified columns", "Data Transformation"),
            ("rename-columns", "Rename columns using mapping", "Data Transformation"),
            ("reorder-columns", "Reorder columns in specified order", "Data Transformation"),
            ("scale", "Scale numeric columns", "Data Scaling"),
            ("replace", "Replace values in columns", "Sampling"),
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
