# API Reference

Programmatic usage guide for integrating FilePrepper into .NET applications.

## Installation

```bash
dotnet add package FilePrepper
```

## Basic Usage Pattern

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
