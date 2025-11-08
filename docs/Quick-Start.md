# Quick Start Guide

Get started with FilePrepper in 5 minutes - either as a CLI tool or SDK.

## Installation

### Option 1: CLI Tool (No Coding Required)

```bash
# Install globally
dotnet tool install -g fileprepper-cli

# Verify installation
fileprepper --version
# Expected: 0.4.1

fileprepper --help
```

### Option 2: SDK Library (Programmatic Use)

```bash
# Add to your project
dotnet add package FilePrepper

# Or in .csproj
<PackageReference Include="FilePrepper" Version="0.4.1" />
```

### Option 3: Build from Source

```bash
git clone https://github.com/iyulab/FilePrepper.git
cd FilePrepper
dotnet build src/FilePrepper.sln

# Run CLI from source
cd src/FilePrepper.CLI
dotnet run -- --help
```

## Your First Command

### 1. Prepare Sample Data

Create `sample.csv`:
```csv
Name,Age,Salary,Department
Alice,25,50000,Engineering
Bob,30,75000,Sales
Charlie,35,60000,Engineering
David,28,,Sales
Eve,32,70000,Marketing
```

### 2. Normalize Numeric Columns

```bash
# If installed as global tool
fileprepper normalize -i sample.csv -o output.csv -c "Age,Salary" -m MinMax

# Or if running from source
cd src/FilePrepper.CLI
dotnet run -- normalize -i sample.csv -o output.csv -c "Age,Salary" -m MinMax
```

**Result:**
```csv
Name,Age,Salary,Department
Alice,0,0,Engineering
Bob,0.5,1,Sales
Charlie,1,0.4,Engineering
David,0.3,0,Sales
Eve,0.7,0.8,Marketing
```

### 3. Fill Missing Values

```bash
fileprepper fill-missing -i sample.csv -o output.csv -c "Salary" -m Mean
```

## Common Tasks

### Data Cleaning

```bash
# Remove unnecessary columns
fileprepper remove-columns -i data.csv -o clean.csv -c "TempCol,Debug,Notes"

# Drop duplicates
fileprepper drop-duplicates -i data.csv -o unique.csv -c "Email"

# Fill missing values
fileprepper fill-missing -i data.csv -o filled.csv -c "Age,Salary" -m Mean
```

### Data Transformation

```bash
# Convert types
fileprepper convert-type -i data.csv -o typed.csv \
  -c "Date:DateTime:yyyy-MM-dd,Age:Integer"

# Normalize values
fileprepper normalize -i data.csv -o norm.csv -c "Price,Quantity" -m MinMax

# Extract date components
fileprepper extract-date -i data.csv -o dated.csv \
  --column "OrderDate" --components "Year,Month,Day"
```

### Data Analysis

```bash
# Calculate statistics
fileprepper stats -i data.csv -o stats.csv -c "Age,Salary,Score"

# Group and aggregate
fileprepper aggregate -i sales.csv -o summary.csv \
  --group "Region,Product" \
  --aggregations "Sales:Sum,Quantity:Avg"
```

### File Operations

```bash
# Merge files vertically
fileprepper merge file1.csv file2.csv file3.csv -o merged.csv -t Vertical

# Convert format
fileprepper convert-format -i data.csv -o data.json -f JSON
```

## Multi-Column Processing

Process multiple columns in a single command:

```bash
# Normalize 5 columns simultaneously
fileprepper normalize -i data.csv -o output.csv \
  -c "Col1,Col2,Col3,Col4,Col5" -m ZScore

# Convert 3 types at once
fileprepper convert-type -i data.csv -o output.csv \
  -c "Date:DateTime,Age:Integer,Price:Decimal"

# Fill missing in multiple columns
fileprepper fill-missing -i data.csv -o output.csv \
  -c "Age,Salary,Score" -m Median
```

## Building a Pipeline

### CLI Approach (Multiple File I/O)

Combine commands for complex workflows:

```bash
# Step 1: Clean data
fileprepper fill-missing -i raw.csv -o step1.csv -c "Age,Salary" -m Mean

# Step 2: Remove outliers
fileprepper filter-rows -i step1.csv -o step2.csv \
  --column "Age" --operator LessThan --value "100"

# Step 3: Normalize
fileprepper normalize -i step2.csv -o step3.csv -c "Age,Salary" -m MinMax

# Step 4: Convert to JSON
fileprepper convert-format -i step3.csv -o final.json -f JSON
# Total: 8 file I/O operations (4 reads + 4 writes)
```

### SDK Approach (Efficient - 67-90% Less I/O)

Use the Pipeline API for in-memory processing:

```csharp
using FilePrepper.Pipeline;

// Same workflow with only 2 file I/O operations!
await DataPipeline
    .FromCsvAsync("raw.csv")
    .FillMissing(columns: new[] { "Age", "Salary" }, method: FillMethod.Mean)
    .FilterRows(row => int.Parse(row["Age"]) < 100)
    .Normalize(columns: new[] { "Age", "Salary" }, method: NormalizationMethod.MinMax)
    .ToCsvAsync("final.csv");  // Or .ToJson() for JSON output

// 75% reduction in file I/O!
```

### SDK Quick Examples

**ML Feature Engineering**:
```csharp
using FilePrepper.Pipeline;

var result = await DataPipeline
    .FromCsvAsync("data.csv")
    .AddColumn("AgeGroup", row =>
        int.Parse(row["Age"]) < 30 ? "Young" : "Senior")
    .Normalize(columns: new[] { "Age", "Salary" },
               method: NormalizationMethod.MinMax)
    .ToDataFrame();

Console.WriteLine($"Processed {result.RowCount} rows");
```

**In-Memory Processing (Zero File I/O)**:
```csharp
var data = new List<Dictionary<string, string>>
{
    new() { ["Name"] = "Alice", ["Age"] = "25", ["Salary"] = "50000" },
    new() { ["Name"] = "Bob", ["Age"] = "30", ["Salary"] = "60000" }
};

var processed = DataPipeline
    .FromData(data)
    .Normalize(columns: new[] { "Age", "Salary" },
               method: NormalizationMethod.MinMax)
    .FilterRows(row => double.Parse(row["Age"]) > 0.5)
    .ToDataFrame();

// Access results directly - no files needed!
foreach (var row in processed.Rows)
{
    Console.WriteLine($"{row["Name"]}: Age={row["Age"]}, Salary={row["Salary"]}");
}
```

## Error Handling

Handle dirty data gracefully:

```bash
# Ignore errors and use defaults
fileprepper normalize -i messy.csv -o clean.csv \
  -c "Age,Salary" -m MinMax \
  --ignore-errors --default-value "0"
```

## Getting Help

### General Help
```bash
fileprepper --help
```

### Command-Specific Help
```bash
fileprepper normalize --help
fileprepper aggregate --help
```

### List All Commands
```bash
fileprepper --help | grep "  "
```

## Next Steps

- [CLI Reference](CLI-Guide.md) - Complete command reference
- [Common Scenarios](Common-Scenarios.md) - Real-world examples
- [API Reference](API-Reference.md) - Programmatic usage

## Tips for Success

1. **Test on small files first** - Verify your command works
2. **Use `--help`** - Every command has detailed documentation
3. **Check headers** - Ensure `--has-header` matches your file
4. **Quote column names** - Use quotes for names with spaces
5. **Backup originals** - Keep copies before transforming

## Troubleshooting

### "Command not found"
Install as global tool:
```bash
# Install globally
dotnet tool install -g FilePrepper.CLI

# Or run from source
cd src/FilePrepper.CLI
dotnet run -- <command>
```

### "Column not found"
- Check column names match exactly (case-sensitive)
- Verify header row exists with `--has-header true`
- Use quotes for column names with spaces

### "Out of memory"
- Process file in smaller batches
- Use appropriate data types
- Consider streaming solutions for very large files
