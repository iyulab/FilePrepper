# FilePrepper

[![NuGet Version](https://img.shields.io/nuget/v/fileprepper-cli?label=CLI&logo=nuget&color=blue)](https://www.nuget.org/packages/fileprepper-cli)
[![NuGet Downloads](https://img.shields.io/nuget/dt/fileprepper-cli?label=downloads&logo=nuget&color=blue)](https://www.nuget.org/packages/fileprepper-cli)
[![.NET Version](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)](https://github.com/iyulab/FilePrepper)
[![Tests](https://img.shields.io/badge/tests-159%20passing-brightgreen)](https://github.com/iyulab/FilePrepper)

A powerful .NET library and CLI tool for CSV/tabular data processing. Process data files without writing code, or integrate into your .NET applications.

## Quick Start

### CLI Usage (No Coding Required)

```bash
# Normalize multiple columns
fileprepper normalize -i data.csv -o output.csv -c "Age,Salary,Score" -m MinMax

# Convert data types
fileprepper convert-type -i data.csv -o output.csv -c "Date:DateTime:yyyy-MM-dd,Age:Integer"

# Fill missing values
fileprepper fill-missing -i data.csv -o output.csv -c "Age,Salary" -m Mean

# Get help for any command
fileprepper normalize --help
```

### Library Usage

```csharp
var options = new NormalizeDataOption
{
    InputPath = "data.csv",
    OutputPath = "output.csv",
    TargetColumns = new[] { "Age", "Salary" },
    Method = NormalizationMethod.MinMax
};

var task = new NormalizeDataTask(logger);
await task.ExecuteAsync(new TaskContext(options));
```

## Features

### 20+ Data Operations
- **Manipulation**: Add/Remove/Rename/Reorder columns, Column interactions
- **Transformation**: Type conversion, Date extraction, Normalization, Scaling, One-hot encoding
- **Analysis**: Aggregation, Statistics, Sampling
- **Organization**: Deduplication, Filtering, Merging
- **Format**: CSV/TSV/JSON/XML conversion, Excel support

### Key Capabilities
✅ **Multi-column processing** - Process multiple columns in single command
✅ **Type-safe** - Compile-time checking for .NET integration
✅ **CLI & Library** - Use without coding or integrate programmatically
✅ **Error handling** - Flexible error strategies with logging
✅ **Well-tested** - 159 unit tests ensuring reliability

## Installation

### CLI Tool (Recommended)
```bash
# Install as global .NET tool
dotnet tool install -g FilePrepper.CLI

# Use anywhere
fileprepper --help
fileprepper normalize -i data.csv -o output.csv -c "Age,Salary" -m MinMax
```

### Library (NuGet)
```bash
dotnet add package FilePrepper
```

See [INSTALL.md](INSTALL.md) for detailed installation instructions.

## Documentation

- [Quick Start Guide](docs/Quick-Start.md) - Get running in 5 minutes
- [CLI Reference](docs/CLI-Guide.md) - Complete command reference
- [Common Scenarios](docs/Common-Scenarios.md) - Real-world examples
- [API Reference](docs/API-Reference.md) - Programmatic usage

## Examples

### Multi-Column Operations
```bash
# Process 3 columns simultaneously
fileprepper normalize -i sales.csv -o normalized.csv \
  -c "Price,Quantity,Revenue" -m MinMax --min 0 --max 1
```

### Data Pipeline
```bash
# 1. Fill missing values
fileprepper fill-missing -i raw.csv -o step1.csv -c "Age" -m Mean

# 2. Normalize
fileprepper normalize -i step1.csv -o step2.csv -c "Age,Salary" -m MinMax

# 3. Convert format
fileprepper convert-format -i step2.csv -o final.json -f JSON
```

### Programmatic Usage
```csharp
// Configure multiple column normalization
var options = new NormalizeDataOption
{
    InputPath = "data.csv",
    OutputPath = "output.csv",
    TargetColumns = new[] { "Col1", "Col2", "Col3" },
    Method = NormalizationMethod.ZScore,
    IgnoreErrors = true
};

// Execute
var task = new NormalizeDataTask(logger);
var result = await task.ExecuteAsync(new TaskContext(options));
```

## Requirements

- .NET 9.0 or later
- Cross-platform (Windows, Linux, macOS)