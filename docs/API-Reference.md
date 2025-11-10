# API Reference

**Version**: 0.4.4

Programmatic usage guide for integrating FilePrepper into .NET applications.

## Installation

```bash
dotnet add package FilePrepper
```

## Recommended: Pipeline API (v0.4.0+)

The Pipeline API provides a fluent interface for efficient data preprocessing with **67-90% reduction in file I/O**.

### Quick Example

```csharp
using FilePrepper.Pipeline;

await DataPipeline
    .FromCsvAsync("data.csv")
    .Normalize(columns: new[] { "Age", "Salary" }, method: NormalizationMethod.MinMax)
    .FillMissing(columns: new[] { "Score" }, method: FillMethod.Mean)
    .FilterRows(row => int.Parse(row["Age"]) >= 18)
    .ToCsvAsync("output.csv");
```

### DataPipeline Class

**Factory Methods**:
```csharp
// From CSV file
Task<DataPipeline> FromCsvAsync(string path, bool hasHeader = true)

// From Excel file (.xls, .xlsx)
Task<DataPipeline> FromExcelAsync(string path, bool hasHeader = true, string? sheetName = null, int sheetIndex = 0)

// From JSON file (array of objects)
Task<DataPipeline> FromJsonAsync(string path)

// From XML file (simple flat structure: root/row/column)
Task<DataPipeline> FromXmlAsync(string path, string rowElement = "row")

// From in-memory data
DataPipeline FromData(IEnumerable<Dictionary<string, string>> data)
```

**Transformation Methods** (all return `this` for chaining):
```csharp
DataPipeline AddColumn(string columnName, Func<Dictionary<string, string>, string> valueSelector)
DataPipeline RemoveColumns(string[] columnNames)
DataPipeline RenameColumn(string oldName, string newName)
DataPipeline FilterRows(Func<Dictionary<string, string>, bool> predicate)
DataPipeline Normalize(string[] columns, NormalizationMethod method, double minValue = 0, double maxValue = 1)
DataPipeline FillMissing(string[] columns, FillMethod method, string? constantValue = null)

// NEW in v0.4.4: Advanced Analytics
GroupedDataPipeline GroupBy(string keyColumn)
DataPipeline Join(DataPipeline right, string leftKey, string rightKey, JoinType joinType = JoinType.Inner,
                  string[]? selectColumns = null, string? leftPrefix = null, string? rightPrefix = null)
ColumnStatistics GetStatistics(string column)
DataPipeline Normalize(string column, NormalizationMethod method, string? outputColumn = null)
```

**Output Methods**:
```csharp
DataFrame ToDataFrame()  // Get immutable snapshot
Task ToCsvAsync(string path, bool hasHeader = true)
Task ToExcelAsync(string path, bool hasHeader = true, string sheetName = "Sheet1")
Task ToJsonAsync(string path, bool indented = true)
Task ToXmlAsync(string path, string rootElement = "data", string rowElement = "row")
IEnumerable<string> GetColumn(string columnName)
```

**Enums**:
```csharp
public enum NormalizationMethod { MinMax, ZScore, Robust }  // Robust added in v0.4.4
public enum FillMethod { Mean, Median, Mode, Forward, Backward, Constant }
public enum JoinType { Inner, Left, Right, Outer }  // NEW in v0.4.4
public enum AggregationMethod {   // Extended in v0.4.4
    Mean, Sum, Min, Max, Count, Std,
    Var, Median, First, Last
}
```

### GroupedDataPipeline Class (NEW in v0.4.4)

Represents data grouped by a key column, ready for aggregation:

```csharp
public class GroupedDataPipeline
{
    DataPipeline Aggregate(
        (string column, AggregationMethod method)[] aggregations,
        bool keepKey = true,
        string? suffixFormat = "_{method}")
}
```

### ColumnStatistics Record (NEW in v0.4.4)

Comprehensive statistical summary for numeric columns:

```csharp
public record ColumnStatistics
{
    double Mean { get; }
    double Std { get; }           // Sample standard deviation
    double Min { get; }
    double Max { get; }
    double Median { get; }
    double Q1 { get; }            // 25th percentile
    double Q3 { get; }            // 75th percentile
    double IQR { get; }           // Interquartile range (Q3 - Q1)
    int Count { get; }            // Valid numeric values
    int NullCount { get; }        // Null/non-numeric values
    double Variance { get; }      // Sample variance
}
```

### DataFrame Class

Immutable data container for inspection:

```csharp
public class DataFrame
{
    IReadOnlyList<Dictionary<string, string>> Rows { get; }
    IReadOnlyList<string> ColumnNames { get; }
    int RowCount { get; }
    int ColumnCount { get; }

    IEnumerable<string> GetColumn(string columnName)
    DataFrame Select(params string[] columnNames)
    DataFrame Where(Func<Dictionary<string, string>, bool> predicate)
    string ToCsv()
    string ToJson()
}
```

### Pipeline Examples

**ML Feature Engineering**:
```csharp
var result = await DataPipeline
    .FromCsvAsync("orders.csv")
    .AddColumn("Year", row => DateTime.Parse(row["OrderDate"]).Year.ToString())
    .AddColumn("Month", row => DateTime.Parse(row["OrderDate"]).Month.ToString())
    .Normalize(columns: new[] { "Revenue", "Quantity" }, method: NormalizationMethod.MinMax)
    .FilterRows(row => int.Parse(row["Year"]) >= 2023)
    .ToDataFrame();
```

**In-Memory Processing**:
```csharp
var data = new List<Dictionary<string, string>>
{
    new() { ["Name"] = "Alice", ["Age"] = "25" },
    new() { ["Name"] = "Bob", ["Age"] = "30" }
};

var processed = DataPipeline
    .FromData(data)
    .Normalize(columns: new[] { "Age" }, method: NormalizationMethod.MinMax)
    .ToDataFrame();
```

**Multi-Format Processing - Excel to JSON**:
```csharp
await DataPipeline
    .FromExcelAsync("sales.xlsx", sheetName: "Q4_Data")
    .AddColumn("Total", row =>
        (double.Parse(row["Price"]) * double.Parse(row["Quantity"])).ToString())
    .FilterRows(row => double.Parse(row["Total"]) >= 1000)
    .ToJsonAsync("high_value_sales.json");
```

**Multi-Format Processing - JSON to Excel**:
```csharp
await DataPipeline
    .FromJsonAsync("api_response.json")
    .Normalize(columns: new[] { "Score", "Rating" }, method: NormalizationMethod.MinMax)
    .ToExcelAsync("normalized_data.xlsx", sheetName: "Results");
```

**Multi-Format Processing - XML to CSV**:
```csharp
await DataPipeline
    .FromXmlAsync("legacy_data.xml", rowElement: "record")
    .RenameColumn("OldName", "NewName")
    .RemoveColumns(new[] { "ObsoleteField" })
    .ToCsvAsync("modernized_data.csv");
```

**Convert to All Formats**:
```csharp
var pipeline = await DataPipeline.FromCsvAsync("source.csv");
await pipeline.ToExcelAsync("output.xlsx");
await pipeline.ToJsonAsync("output.json");
await pipeline.ToXmlAsync("output.xml");
```

**GroupBy/Aggregate (NEW in v0.4.4)** - Time-series batch aggregation:
```csharp
var aggregated = await DataPipeline
    .FromCsvAsync("sensor_data.csv")
    .GroupBy("batch_id")
    .Aggregate(new[]
    {
        ("temperature", AggregationMethod.Mean),
        ("temperature", AggregationMethod.Std),
        ("pressure", AggregationMethod.Min),
        ("pressure", AggregationMethod.Max),
        ("duration", AggregationMethod.Sum)
    });

// Result columns: batch_id, temperature_mean, temperature_std,
//                 pressure_min, pressure_max, duration_sum
```

**Join Operations (NEW in v0.4.4)** - Combine multiple data sources:
```csharp
var sensorData = await DataPipeline.FromCsvAsync("sensors.csv");
var qualityLabels = await DataPipeline.FromCsvAsync("quality.csv");

// Inner join
var joined = sensorData.Join(
    qualityLabels,
    leftKey: "batch_id",
    rightKey: "batch_id",
    joinType: JoinType.Inner,
    selectColumns: new[] { "defect_rate", "quality_score" }
);

// Left join with prefixes (avoid column collision)
var leftJoined = sensorData.Join(
    qualityLabels,
    leftKey: "id",
    rightKey: "sensor_id",
    joinType: JoinType.Left,
    leftPrefix: "sensor_",
    rightPrefix: "quality_"
);
```

**Statistical Analysis (NEW in v0.4.4)** - Data exploration:
```csharp
var data = await DataPipeline.FromCsvAsync("measurements.csv");

// Get comprehensive statistics
var stats = data.GetStatistics("temperature");
Console.WriteLine($"Mean: {stats.Mean}, Std: {stats.Std}");
Console.WriteLine($"Median: {stats.Median}, IQR: {stats.IQR}");
Console.WriteLine($"Range: [{stats.Min}, {stats.Max}]");
Console.WriteLine($"Valid: {stats.Count}, Missing: {stats.NullCount}");

// Normalize data with different methods
var normalized = data
    .Normalize("temperature", NormalizationMethod.ZScore)      // Mean=0, Std=1
    .Normalize("pressure", NormalizationMethod.MinMax)         // [0, 1]
    .Normalize("humidity", NormalizationMethod.Robust);        // Robust to outliers
```

**Complete ML Pipeline (v0.4.4)** - Full preprocessing workflow:
```csharp
var result = await DataPipeline
    .FromCsvAsync("raw_sensor_data.csv")
    // 1. Aggregate by batch
    .GroupBy("batch_id")
    .Aggregate(new[]
    {
        ("temp_zone1", AggregationMethod.Mean),
        ("temp_zone1", AggregationMethod.Std),
        ("temp_zone2", AggregationMethod.Mean),
        ("pressure", AggregationMethod.Max)
    })
    // 2. Join with quality labels
    .Join(
        await DataPipeline.FromCsvAsync("quality_labels.csv"),
        leftKey: "batch_id",
        rightKey: "batch_id",
        joinType: JoinType.Inner,
        selectColumns: new[] { "defect_rate", "quality_score" }
    )
    // 3. Normalize features
    .Normalize("temp_zone1_mean", NormalizationMethod.ZScore)
    .Normalize("temp_zone2_mean", NormalizationMethod.ZScore)
    .Normalize("pressure_max", NormalizationMethod.MinMax)
    // 4. Export for ML training
    .ToCsvAsync("ml_ready_dataset.csv");
```

## Traditional: Task API (Backward Compatible)

All tasks follow a consistent pattern:

1. Create options object
2. Instantiate task with logger
3. Execute with TaskContext

```csharp
using FilePrepper.Tasks.NormalizeData;
using Microsoft.Extensions.Logging;

// 1. Configure options
var options = new NormalizeDataOption
{
    InputPath = "input.csv",
    OutputPath = "output.csv",
    TargetColumns = new[] { "Age", "Salary" },
    Method = NormalizationMethod.MinMax,
    MinValue = 0,
    MaxValue = 1
};

// 2. Create task
var logger = loggerFactory.CreateLogger<NormalizeDataTask>();
var task = new NormalizeDataTask(logger);

// 3. Execute
var context = new TaskContext(options);
bool success = await task.ExecuteAsync(context);
```

## Core Interfaces

### ITask
```csharp
public interface ITask
{
    Task<bool> ExecuteAsync(TaskContext context);
    bool Execute(TaskContext context);
}
```

### ITaskOption
```csharp
public interface ITaskOption
{
    string InputPath { get; set; }
    string OutputPath { get; set; }
    bool HasHeader { get; set; }
    bool IgnoreErrors { get; set; }
    string[] Validate();
}
```

### TaskContext
```csharp
public class TaskContext
{
    public ITaskOption Options { get; }
    public TaskContext(ITaskOption options) { ... }
}
```

## Task Reference

### Data Manipulation

#### AddColumnsTask

```csharp
using FilePrepper.Tasks.AddColumns;

var options = new AddColumnsOption
{
    InputPath = "data.csv",
    OutputPath = "output.csv",
    NewColumns = new Dictionary<string, string>
    {
        { "Status", "Active" },
        { "Priority", "High" }
    }
};

var task = new AddColumnsTask(logger);
await task.ExecuteAsync(new TaskContext(options));
```

#### RemoveColumnsTask

```csharp
using FilePrepper.Tasks.RemoveColumns;

var options = new RemoveColumnsOption
{
    InputPath = "data.csv",
    OutputPath = "output.csv",
    TargetColumns = new[] { "TempCol", "Debug", "Notes" }
};
```

#### RenameColumnsTask

```csharp
using FilePrepper.Tasks.RenameColumns;

var options = new RenameColumnsOption
{
    InputPath = "data.csv",
    OutputPath = "output.csv",
    ColumnMapping = new Dictionary<string, string>
    {
        { "OldName", "NewName" },
        { "Age", "Years" }
    }
};
```

#### ReorderColumnsTask

```csharp
using FilePrepper.Tasks.ReorderColumns;

var options = new ReorderColumnsOption
{
    InputPath = "data.csv",
    OutputPath = "output.csv",
    Order = new List<string> { "ID", "Name", "Email", "Age" }
};
```

### Data Transformation

#### DataTypeConvertTask

```csharp
using FilePrepper.Tasks.DataTypeConvert;

var options = new DataTypeConvertOption
{
    InputPath = "data.csv",
    OutputPath = "output.csv",
    Conversions = new List<ColumnTypeConversion>
    {
        new()
        {
            ColumnName = "Date",
            TargetType = DataType.DateTime,
            Format = "yyyy-MM-dd"
        },
        new()
        {
            ColumnName = "Age",
            TargetType = DataType.Integer
        },
        new()
        {
            ColumnName = "Price",
            TargetType = DataType.Decimal
        }
    },
    Culture = CultureInfo.GetCultureInfo("en-US")
};
```

**DataType Enum:** String, Integer, Decimal, DateTime, Boolean

#### NormalizeDataTask

```csharp
using FilePrepper.Tasks.NormalizeData;

var options = new NormalizeDataOption
{
    InputPath = "data.csv",
    OutputPath = "output.csv",
    TargetColumns = new[] { "Age", "Salary", "Score" },
    Method = NormalizationMethod.MinMax,
    MinValue = 0,
    MaxValue = 1
};
```

**NormalizationMethod:** MinMax, ZScore

#### ScaleDataTask

```csharp
using FilePrepper.Tasks.ScaleData;

var options = new ScaleDataOption
{
    InputPath = "data.csv",
    OutputPath = "output.csv",
    TargetColumns = new[] { "Height", "Weight" },
    Method = ScalingMethod.Standardization
};
```

**ScalingMethod:** MinMax, Standardization

#### FillMissingValuesTask

```csharp
using FilePrepper.Tasks.FillMissingValues;

var options = new FillMissingValuesOption
{
    InputPath = "data.csv",
    OutputPath = "output.csv",
    TargetColumns = new[] { "Age", "Salary" },
    Method = FillMethod.Mean,
    ConstantValue = "0"  // For FillMethod.Constant
};
```

**FillMethod:** Mean, Median, Mode, Forward, Backward, Constant

#### DateExtractionTask

```csharp
using FilePrepper.Tasks.DateExtraction;

var options = new DateExtractionOption
{
    InputPath = "data.csv",
    OutputPath = "output.csv",
    Extractions = new List<DateColumnExtraction>
    {
        new()
        {
            SourceColumn = "OrderDate",
            Components = new List<DateComponent>
            {
                DateComponent.Year,
                DateComponent.Month,
                DateComponent.Day,
                DateComponent.DayOfWeek
            },
            OutputColumnTemplate = "{column}_{component}"
        }
    }
};
```

**DateComponent:** Year, Month, Day, Hour, Minute, Second, DayOfWeek, DayOfYear, Quarter, WeekOfYear

### Data Analysis

#### AggregateTask

```csharp
using FilePrepper.Tasks.Aggregate;

var options = new AggregateOption
{
    InputPath = "sales.csv",
    OutputPath = "summary.csv",
    GroupByColumns = new[] { "Region", "Category" },
    Aggregations = new List<AggregationColumn>
    {
        new()
        {
            SourceColumn = "Sales",
            Function = AggregateFunction.Sum,
            OutputColumn = "TotalSales"
        },
        new()
        {
            SourceColumn = "Quantity",
            Function = AggregateFunction.Average,
            OutputColumn = "AvgQuantity"
        }
    }
};
```

**AggregateFunction:** Sum, Average, Count, Min, Max

#### BasicStatisticsTask

```csharp
using FilePrepper.Tasks.BasicStatistics;

var options = new BasicStatisticsOption
{
    InputPath = "data.csv",
    OutputPath = "stats.csv",
    TargetColumns = new[] { "Age", "Salary", "Score" }
};
```

**Output includes:** Count, Mean, StdDev, Min, Q1, Median, Q3, Max

### Data Organization

#### DropDuplicatesTask

```csharp
using FilePrepper.Tasks.DropDuplicates;

var options = new DropDuplicatesOption
{
    InputPath = "data.csv",
    OutputPath = "unique.csv",
    TargetColumns = new[] { "Email", "PhoneNumber" },
    KeepFirst = true  // true = keep first, false = keep last
};
```

#### FilterRowsTask

```csharp
using FilePrepper.Tasks.FilterRows;

var options = new FilterRowsOption
{
    InputPath = "data.csv",
    OutputPath = "filtered.csv",
    Conditions = new List<FilterCondition>
    {
        new()
        {
            Column = "Age",
            Operator = FilterOperator.GreaterThan,
            Value = "18"
        }
    }
};
```

**FilterOperator:** Equals, NotEquals, GreaterThan, LessThan, GreaterThanOrEqual, LessThanOrEqual, Contains, StartsWith, EndsWith

#### MergeTask

```csharp
using FilePrepper.Tasks.Merge;

// Vertical merge (stack rows)
var verticalOptions = new MergeOption
{
    InputPaths = new[] { "file1.csv", "file2.csv", "file3.csv" },
    OutputPath = "merged.csv",
    MergeType = MergeType.Vertical
};

// Horizontal merge (join)
var horizontalOptions = new MergeOption
{
    InputPaths = new[] { "customers.csv", "orders.csv" },
    OutputPath = "joined.csv",
    MergeType = MergeType.Horizontal,
    JoinType = JoinType.Left,
    JoinKeys = new List<JoinKey>
    {
        new() { Name = "CustomerID" }
    }
};
```

**MergeType:** Vertical, Horizontal
**JoinType:** Inner, Left, Right, Outer

### Error Handling

All tasks support error handling options:

```csharp
var options = new NormalizeDataOption
{
    InputPath = "data.csv",
    OutputPath = "output.csv",
    TargetColumns = new[] { "Age", "Salary" },
    Method = NormalizationMethod.MinMax,

    // Error handling
    IgnoreErrors = true,
    DefaultValue = "0"
};
```

### Validation

Options are validated before execution:

```csharp
var options = new NormalizeDataOption
{
    TargetColumns = new string[0]  // Invalid!
};

string[] errors = options.Validate();
if (errors.Length > 0)
{
    foreach (var error in errors)
        Console.WriteLine($"Validation error: {error}");
}
```

## Dependency Injection

Integrate with ASP.NET Core or other DI frameworks:

```csharp
// Startup.cs or Program.cs
services.AddTransient<NormalizeDataTask>();
services.AddTransient<FillMissingValuesTask>();
// ... register other tasks

// Usage in controller/service
public class DataProcessingService
{
    private readonly NormalizeDataTask _normalizeTask;
    private readonly ILogger<DataProcessingService> _logger;

    public DataProcessingService(
        NormalizeDataTask normalizeTask,
        ILogger<DataProcessingService> logger)
    {
        _normalizeTask = normalizeTask;
        _logger = logger;
    }

    public async Task<bool> ProcessData(string inputPath, string outputPath)
    {
        var options = new NormalizeDataOption
        {
            InputPath = inputPath,
            OutputPath = outputPath,
            TargetColumns = new[] { "Age", "Salary" },
            Method = NormalizationMethod.MinMax
        };

        return await _normalizeTask.ExecuteAsync(new TaskContext(options));
    }
}
```

## Azure Functions Example

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using FilePrepper.Tasks.NormalizeData;

public class DataProcessor
{
    private readonly ILogger<DataProcessor> _logger;

    public DataProcessor(ILogger<DataProcessor> logger)
    {
        _logger = logger;
    }

    [Function("ProcessData")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        // Get file from request
        var formData = await req.ReadFormAsync();
        var file = formData.Files["file"];

        // Save to temp location
        var inputPath = Path.GetTempFileName();
        var outputPath = Path.GetTempFileName();

        using (var stream = file.OpenReadStream())
        using (var fileStream = File.Create(inputPath))
        {
            await stream.CopyToAsync(fileStream);
        }

        // Process with FilePrepper
        var options = new NormalizeDataOption
        {
            InputPath = inputPath,
            OutputPath = outputPath,
            TargetColumns = new[] { "Age", "Salary" },
            Method = NormalizationMethod.MinMax
        };

        var task = new NormalizeDataTask(_logger);
        bool success = await task.ExecuteAsync(new TaskContext(options));

        if (!success)
        {
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            return response;
        }

        // Return processed file
        var result = req.CreateResponse(HttpStatusCode.OK);
        result.Headers.Add("Content-Type", "text/csv");

        var bytes = await File.ReadAllBytesAsync(outputPath);
        await result.Body.WriteAsync(bytes);

        // Cleanup
        File.Delete(inputPath);
        File.Delete(outputPath);

        return result;
    }
}
```

## Best Practices

1. **Use DI** - Register tasks in DI container
2. **Validate options** - Call `Validate()` before execution
3. **Handle errors** - Check return value and log errors
4. **Dispose resources** - Tasks handle file cleanup automatically
5. **Use async** - Prefer `ExecuteAsync` over `Execute`
6. **Log appropriately** - Provide logger for diagnostics
7. **Test with small data** - Verify logic before large files

## Performance Considerations

```csharp
// For large files, consider:
1. Appropriate data types (Integer vs Decimal)
2. Error handling strategy (IgnoreErrors vs fail fast)
3. Memory availability (tasks load files into memory)
4. Batch processing for very large datasets
```

## Further Reading

- [CLI Guide](CLI-Guide.md) - Command-line usage
- [Common Scenarios](Common-Scenarios.md) - Practical examples
- [Quick Start](Quick-Start.md) - Getting started guide
