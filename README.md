# FilePrepper

[![NuGet Version](https://img.shields.io/nuget/v/fileprepper-cli?label=CLI&logo=nuget&color=blue)](https://www.nuget.org/packages/fileprepper-cli)
[![NuGet Downloads](https://img.shields.io/nuget/dt/fileprepper-cli?label=downloads&logo=nuget&color=blue)](https://www.nuget.org/packages/fileprepper-cli)
[![.NET Version](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

A powerful .NET CLI tool for data preprocessing without coding. Perfect for ML data preparation, ETL pipelines, and data analysis workflows.

## 🚀 Quick Start

### Installation

```bash
# Install as global .NET tool
dotnet tool install -g fileprepper-cli

# Verify installation
fileprepper --version
```

### Basic Usage

```bash
# Normalize numeric columns
fileprepper normalize-data --input data.csv --output normalized.csv \
  --columns "Age,Salary,Score" --method MinMax

# Fill missing values
fileprepper fill-missing-values --input data.csv --output filled.csv \
  --columns "Age,Salary" --method Mean

# Filter rows
fileprepper filter-rows --input sales.csv --output filtered.csv \
  --conditions "Revenue:GreaterThan:1000,Region:Equals:North"

# Convert file formats
fileprepper file-format-convert --input data.csv --output data.json \
  --format JSON

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

### Data Cleaning Pipeline

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

### ML Feature Engineering

```bash
# Extract date features
fileprepper date-extraction --input orders.csv --output features1.csv \
  --columns "OrderDate" --features Year,Month,DayOfWeek

# Create lag features for time series
fileprepper create-lag-features --input sales.csv --output features2.csv \
  --group-by ProductID --lag-columns Revenue \
  --periods 1,2,3,7 --sort-by Date

# One-hot encode categorical variables
fileprepper one-hot-encoding --input features2.csv --output features3.csv \
  --columns "Category,Region"

# Create interaction features
fileprepper column-interaction --input features3.csv --output final.csv \
  --column-pairs "Price*Quantity,Age*Income"
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

## 🔧 Programmatic Usage

FilePrepper can also be used as a .NET library:

```bash
dotnet add package FilePrepper
```

```csharp
using FilePrepper.Tasks.NormalizeData;
using Microsoft.Extensions.Logging;

var options = new NormalizeDataOption
{
    InputPath = "data.csv",
    OutputPath = "normalized.csv",
    TargetColumns = new[] { "Age", "Salary", "Score" },
    Method = NormalizationMethod.MinMax,
    MinValue = 0,
    MaxValue = 1
};

var task = new NormalizeDataTask(logger);
var context = new TaskContext(options);
bool success = await task.ExecuteAsync(context);
```

See [API Reference](docs/API-Reference.md) for detailed programmatic usage.

## 📖 Documentation

- **[Quick Start Guide](docs/Quick-Start.md)** - Get started in 5 minutes
- **[CLI Guide](docs/CLI-Guide.md)** - Complete command reference
- **[Common Scenarios](docs/Common-Scenarios.md)** - Real-world use cases
- **[API Reference](docs/API-Reference.md)** - Programmatic usage
- **[Installation Guide](INSTALL.md)** - Detailed installation

For more documentation, see the [docs/](docs/) directory.

## 🎯 Use Cases

- **Machine Learning** - Prepare datasets for training (normalization, encoding, feature engineering)
- **Data Analysis** - Clean and transform data for analysis
- **ETL Pipelines** - Extract, transform, and load data workflows
- **Data Migration** - Convert between formats and clean legacy data
- **Automation** - Script data processing without custom code

## 📋 Requirements

- **.NET 9.0** or later
- **Cross-platform** - Windows, Linux, macOS
- **No coding required** - Command-line only (or use as library)

## 🤝 Contributing

Contributions are welcome! Please feel free to submit issues, feature requests, or pull requests.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🔗 Links

- **NuGet Package**: https://www.nuget.org/packages/fileprepper-cli
- **GitHub Repository**: https://github.com/iyulab/FilePrepper
- **Issues**: https://github.com/iyulab/FilePrepper/issues
- **Documentation**: [docs/](docs/)
- **Changelog**: [CHANGELOG.md](CHANGELOG.md)

---

**Made with ❤️ by iyulab** | ML Data Preprocessing Tool - No Coding Required
