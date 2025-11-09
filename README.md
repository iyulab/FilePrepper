# FilePrepper

[![NuGet SDK](https://img.shields.io/nuget/v/FilePrepper?label=SDK&logo=nuget&color=blue)](https://www.nuget.org/packages/FilePrepper)
[![NuGet CLI](https://img.shields.io/nuget/v/fileprepper-cli?label=CLI&logo=nuget&color=blue)](https://www.nuget.org/packages/fileprepper-cli)
[![SDK Downloads](https://img.shields.io/nuget/dt/FilePrepper?label=SDK%20Downloads&logo=nuget&color=blue)](https://www.nuget.org/packages/FilePrepper)
[![CLI Downloads](https://img.shields.io/nuget/dt/fileprepper-cli?label=CLI%20Downloads&logo=nuget&color=blue)](https://www.nuget.org/packages/fileprepper-cli)
[![.NET Version](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

A powerful .NET library and CLI tool for data preprocessing. Features a **Pipeline API** for efficient in-memory data transformations with 67-90% reduction in file I/O. Perfect for ML data preparation, ETL pipelines, and data analysis workflows.

## 🚀 Quick Start

### SDK Installation

```bash
# Install FilePrepper SDK for programmatic use
dotnet add package FilePrepper

# Or install CLI tool globally
dotnet tool install -g fileprepper-cli
```

### SDK Usage (Recommended)

```csharp
using FilePrepper.Pipeline;

// CSV Processing: Only 2 file I/O operations (read + write)
await DataPipeline
    .FromCsvAsync("data.csv")
    .Normalize(columns: new[] { "Age", "Salary", "Score" },
               method: NormalizationMethod.MinMax)
    .FillMissing(columns: new[] { "Score" }, method: FillMethod.Mean)
    .FilterRows(row => int.Parse(row["Age"]) >= 30)
    .ToCsvAsync("output.csv");

// Multi-Format Support: Excel → Transform → JSON
await DataPipeline
    .FromExcelAsync("sales.xlsx")
    .AddColumn("Total", row =>
        (double.Parse(row["Price"]) * double.Parse(row["Quantity"])).ToString())
    .FilterRows(row => double.Parse(row["Total"]) >= 1000)
    .ToJsonAsync("high_value_sales.json");
```

### CLI Usage

```bash
# Normalize numeric columns
fileprepper normalize-data --input data.csv --output normalized.csv \
  --columns "Age,Salary,Score" --method MinMax

# Fill missing values
fileprepper fill-missing-values --input data.csv --output filled.csv \
  --columns "Age,Salary" --method Mean

# Get help
fileprepper --help
fileprepper <command> --help
```

## 📦 Supported Formats

Process data in multiple formats:
- **CSV** (Comma-Separated Values)
- **TSV** (Tab-Separated Values)
- **JSON** (JavaScript Object Notation)
- **XML** (Extensible Markup Language)
- **Excel** (XLSX/XLS files)

## 🛠️ Available Commands (20+)

### Data Transformation
- `normalize-data` - Normalize columns (MinMax, ZScore)
- `scale-data` - Scale numeric data (StandardScaler, MinMaxScaler, RobustScaler)
- `one-hot-encoding` - Convert categorical to binary columns
- `data-type-convert` - Convert column data types
- `date-extraction` - Extract date features (Year, Month, Day, DayOfWeek)

### Data Cleaning
- `fill-missing-values` - Fill missing data (Mean, Median, Mode, Forward, Backward, Constant)
- `drop-duplicates` - Remove duplicate rows
- `value-replace` - Replace values in columns

### Column Operations
- `add-columns` - Add new calculated columns
- `remove-columns` - Delete unwanted columns
- `rename-columns` - Rename column headers
- `reorder-columns` - Change column order
- `column-interaction` - Create interaction features

### Data Analysis
- `basic-statistics` - Calculate statistics (Mean, Median, StdDev, ZScore)
- `aggregate` - Group and aggregate data
- `filter-rows` - Filter rows by conditions

### Data Organization
- `merge` - Combine multiple files (Horizontal/Vertical merge)
- `data-sampling` - Sample rows (Random, Stratified, Systematic)
- `file-format-convert` - Convert between formats

### Feature Engineering
- `create-lag-features` - Create time-series lag features

## 💡 Common Use Cases

### Data Cleaning Pipeline (CLI)

```bash
# 1. Remove unnecessary columns
fileprepper remove-columns --input raw.csv --output step1.csv \
  --columns "Debug,TempCol,Notes"

# 2. Drop duplicates
fileprepper drop-duplicates --input step1.csv --output step2.csv \
  --columns "Email" --keep First

# 3. Fill missing values
fileprepper fill-missing-values --input step2.csv --output step3.csv \
  --columns "Age,Salary" --method Mean

# 4. Normalize numeric columns
fileprepper normalize-data --input step3.csv --output clean.csv \
  --columns "Age,Salary,Score" --method MinMax
```

### ML Feature Engineering (SDK - Efficient!)

```csharp
using FilePrepper.Pipeline;

// Single pipeline: Only 2 file I/O operations instead of 8!
await DataPipeline
    .FromCsvAsync("orders.csv")
    .AddColumn("Year", row => DateTime.Parse(row["OrderDate"]).Year.ToString())
    .AddColumn("Month", row => DateTime.Parse(row["OrderDate"]).Month.ToString())
    .Normalize(columns: new[] { "Revenue", "Quantity" },
               method: NormalizationMethod.MinMax)
    .FilterRows(row => int.Parse(row["Year"]) >= 2023)
    .ToCsvAsync("features.csv");

// 67-90% reduction in file I/O compared to CLI approach!
```

### Format Conversion

```bash
# CSV to JSON
fileprepper file-format-convert --input data.csv --output data.json --format JSON

# Excel to CSV
fileprepper file-format-convert --input report.xlsx --output report.csv --format CSV

# CSV to XML
fileprepper file-format-convert --input data.csv --output data.xml --format XML
```

### Data Analysis

```bash
# Calculate statistics
fileprepper basic-statistics --input data.csv --output stats.csv \
  --columns "Age,Salary,Score" --statistics Mean,Median,StdDev,ZScore

# Aggregate by group
fileprepper aggregate --input sales.csv --output summary.csv \
  --group-by "Region,Category" --agg-columns "Revenue:Sum,Quantity:Mean"

# Sample data
fileprepper data-sampling --input large.csv --output sample.csv \
  --method Random --sample-size 1000
```

## 🔧 Programmatic Usage (SDK)

FilePrepper provides a powerful SDK with **Pipeline API** for efficient data processing:

```bash
dotnet add package FilePrepper
```

### ✨ Pipeline API (Recommended)

**Benefits**: 67-90% reduction in file I/O, fluent API, in-memory processing

```csharp
using FilePrepper.Pipeline;
using FilePrepper.Tasks.NormalizeData;

// Efficient: Only 2 file I/O operations (read + write)
await DataPipeline
    .FromCsvAsync("data.csv")
    .Normalize(columns: new[] { "Age", "Salary", "Score" },
               method: NormalizationMethod.MinMax)
    .FillMissing(columns: new[] { "Score" }, method: FillMethod.Mean)
    .FilterRows(row => int.Parse(row["Age"]) >= 30)
    .AddColumn("ProcessedDate", _ => DateTime.Now.ToString())
    .ToCsvAsync("output.csv");

// Or work in-memory without any file I/O
var result = DataPipeline
    .FromData(inMemoryData)
    .Normalize(columns: new[] { "Age", "Salary" },
               method: NormalizationMethod.MinMax)
    .ToDataFrame();  // Get immutable snapshot
```

### Advanced Pipeline Features

```csharp
// Chain multiple transformations
var pipeline = await DataPipeline
    .FromCsvAsync("sales.csv")
    .RemoveColumns(new[] { "Debug", "TempCol" })
    .RenameColumn("OldName", "NewName")
    .AddColumn("Total", row =>
        (double.Parse(row["Price"]) * double.Parse(row["Quantity"])).ToString())
    .FilterRows(row => double.Parse(row["Total"]) > 100)
    .Normalize(columns: new[] { "Total" }, method: NormalizationMethod.MinMax);

// Get intermediate results without file I/O
var dataFrame = pipeline.ToDataFrame();
Console.WriteLine($"Processed {dataFrame.RowCount} rows");

// Continue processing
await pipeline
    .AddColumn("ProcessedAt", _ => DateTime.UtcNow.ToString("o"))
    .ToCsvAsync("output.csv");
```

### In-Memory Processing

```csharp
// Work entirely in memory - zero file I/O
var data = new List<Dictionary<string, string>>
{
    new() { ["Name"] = "Alice", ["Age"] = "25", ["Salary"] = "50000" },
    new() { ["Name"] = "Bob", ["Age"] = "30", ["Salary"] = "60000" }
};

var result = DataPipeline
    .FromData(data)
    .Normalize(columns: new[] { "Age", "Salary" },
               method: NormalizationMethod.MinMax)
    .AddColumn("Category", row =>
        int.Parse(row["Age"]) < 30 ? "Junior" : "Senior")
    .ToDataFrame();

// Access results directly
foreach (var row in result.Rows)
{
    Console.WriteLine($"{row["Name"]}: {row["Category"]}");
}
```

### Traditional Task API

```csharp
using FilePrepper.Tasks.NormalizeData;
using Microsoft.Extensions.Logging;

var options = new NormalizeDataOption
{
    InputPath = "data.csv",
    OutputPath = "normalized.csv",
    TargetColumns = new[] { "Age", "Salary", "Score" },
    Method = NormalizationMethod.MinMax
};

var task = new NormalizeDataTask(logger);
var context = new TaskContext(options);
bool success = await task.ExecuteAsync(context);
```

See [SDK Usage Guide](docs/SDK-Usage-Guide.md) for comprehensive examples and best practices.

## 📖 Documentation

### Getting Started
- **[Quick Start Guide](docs/Quick-Start.md)** - Get started in 5 minutes
- **[CLI Guide](docs/CLI-Guide.md)** - Complete command reference
- **[Installation Guide](INSTALL.md)** - Detailed installation

### SDK & Programming
- **[API Reference](docs/API-Reference.md)** - Pipeline API and Task API reference
- **[Quick Start Guide](docs/Quick-Start.md)** - Get started with SDK in 5 minutes

### Use Cases
- **[Common Scenarios](docs/Common-Scenarios.md)** - Real-world use cases

For more documentation, see the [docs/](docs/) directory.

## 🎯 Use Cases

- **Machine Learning** - Prepare datasets for training (normalization, encoding, feature engineering)
- **Data Analysis** - Clean and transform data for analysis
- **ETL Pipelines** - Extract, transform, and load data workflows with minimal I/O overhead
- **Data Migration** - Convert between formats and clean legacy data
- **Automation** - Script data processing with SDK or CLI
- **In-Memory Processing** - Chain transformations without file I/O costs

## 📋 Requirements

- **.NET 9.0** or later
- **Cross-platform** - Windows, Linux, macOS
- **Flexible Usage** - CLI tool (no coding) or SDK (programmatic)

## 🤝 Contributing

Contributions are welcome! Please feel free to submit issues, feature requests, or pull requests.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🔗 Links

- **SDK NuGet Package**: https://www.nuget.org/packages/FilePrepper
- **CLI NuGet Package**: https://www.nuget.org/packages/fileprepper-cli
- **GitHub Repository**: https://github.com/iyulab/FilePrepper
- **Issues**: https://github.com/iyulab/FilePrepper/issues
- **Documentation**: [docs/](docs/)
- **Changelog**: [CHANGELOG.md](CHANGELOG.md)

---

**Made with ❤️ by iyulab** | Efficient Data Preprocessing - CLI & SDK
