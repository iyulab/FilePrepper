# CLI Reference Guide

Complete command-line reference for FilePrepper v0.4.0+

## General Syntax

```bash
fileprepper <command> [options]

# Get help
fileprepper --help
fileprepper <command> --help
```

## Common Options

Available for all commands:

| Option | Aliases | Description | Default |
|--------|---------|-------------|---------|
| `--input` | `-i` | Input file path | Required |
| `--output` | `-o` | Output file path | Required |
| `--has-header` | `--header` | Input has header row | true |
| `--ignore-errors` | - | Continue on errors | false |
| `--verbose` | `-v` | Enable verbose output | false |

## Supported File Formats

- **CSV** (.csv) - Comma-Separated Values
- **TSV** (.tsv) - Tab-Separated Values
- **JSON** (.json) - JavaScript Object Notation
- **XML** (.xml) - Extensible Markup Language
- **Excel** (.xlsx, .xls) - Microsoft Excel Spreadsheet

---

## Commands

### Data Manipulation

#### add-columns
Add new columns with constant or calculated values.

**Syntax:**
```bash
fileprepper add-columns -i INPUT -o OUTPUT --columns DEFINITIONS [OPTIONS]
```

**Example:**
```bash
# Add Status and Priority columns with fixed values
fileprepper add-columns -i data.csv -o output.csv \
  --columns "Status:Active,Priority:High" --verbose
```

**Options:**
- `-i, --input` - Input file path (required)
- `-o, --output` - Output file path (required)
- `--columns` - Column definitions in format `Name:Value` (comma-separated, required)
- `--has-header` - Input has headers (default: true)
- `--verbose` - Show detailed progress

---

#### remove-columns
Remove specified columns from the dataset.

**Syntax:**
```bash
fileprepper remove-columns -i INPUT -o OUTPUT -c COLUMNS [OPTIONS]
```

**Example:**
```bash
# Remove temporary and debug columns
fileprepper remove-columns -i data.csv -o cleaned.csv \
  -c "Temporary,Notes,DebugInfo" --verbose
```

**Options:**
- `-i, --input` - Input file path (required)
- `-o, --output` - Output file path (required)
- `-c, --columns` - Columns to remove (comma-separated, required)
- `--has-header` - Input has headers (default: true)
- `--verbose` - Show detailed progress

---

#### rename-columns
Rename columns using name mappings.

**Syntax:**
```bash
fileprepper rename-columns -i INPUT -o OUTPUT --map MAPPINGS [OPTIONS]
```

**Example:**
```bash
# Rename columns for clarity
fileprepper rename-columns -i data.csv -o renamed.csv \
  --map "OldName:NewName,user_age:Age,dept:Department" --verbose
```

**Options:**
- `-i, --input` - Input file path (required)
- `-o, --output` - Output file path (required)
- `--map` - Rename mappings in format `OldName:NewName` (comma-separated, required)
- `--has-header` - Input has headers (default: true)
- `--verbose` - Show detailed progress

---

#### reorder-columns
Change the order of columns in the output.

**Syntax:**
```bash
fileprepper reorder-columns -i INPUT -o OUTPUT -c COLUMN_ORDER [OPTIONS]
```

**Example:**
```bash
# Reorder columns to put ID first
fileprepper reorder-columns -i data.csv -o reordered.csv \
  -c "ID,Name,Email,Age,Department" --verbose
```

**Options:**
- `-i, --input` - Input file path (required)
- `-o, --output` - Output file path (required)
- `-c, --columns` - Desired column order (comma-separated, required)
- `--has-header` - Input has headers (default: true)
- `--verbose` - Show detailed progress

---

#### column-interaction
Perform mathematical or string operations between columns.

**Syntax:**
```bash
fileprepper column-interaction -i INPUT -o OUTPUT --source COLUMNS --operation OP --output NAME [OPTIONS]
```

**Example:**
```bash
# Calculate total from price and quantity
fileprepper column-interaction -i sales.csv -o calculated.csv \
  --source "Price,Quantity" --operation Multiply --output "Total" --verbose
```

**Options:**
- `-i, --input` - Input file path (required)
- `-o, --output` - Output file path (required)
- `--source` - Source columns (comma-separated, required)
- `--operation` - Operation: Add, Subtract, Multiply, Divide, Concatenate (required)
- `--output` - Output column name (required)
- `--has-header` - Input has headers (default: true)
- `--verbose` - Show detailed progress

---

### Data Transformation

#### convert-type
Convert column data types with optional format specifications.

**Syntax:**
```bash
fileprepper convert-type -i INPUT -o OUTPUT -c CONVERSIONS [OPTIONS]
```

**Example:**
```bash
# Convert multiple column types
fileprepper convert-type -i data.csv -o typed.csv \
  -c "Date:DateTime:yyyy-MM-dd,Age:Integer,Price:Decimal,Active:Boolean" \
  --culture "en-US" --verbose
```

**Options:**
- `-i, --input` - Input file path (required)
- `-o, --output` - Output file path (required)
- `-c, --conversions` - Conversions in format `Column:Type[:Format]` (comma-separated, required)
- `--culture` - Culture for parsing (default: en-US)
- `--has-header` - Input has headers (default: true)
- `--verbose` - Show detailed progress

**Supported Types:** String, Integer, Decimal, DateTime, Boolean

---

#### extract-date
Extract date components (year, month, day, etc.) from date columns.

**Syntax:**
```bash
fileprepper extract-date -i INPUT -o OUTPUT --column COLUMN --components PARTS [OPTIONS]
```

**Example:**
```bash
# Extract year, month, and day of week from order date
fileprepper extract-date -i orders.csv -o extracted.csv \
  --column "OrderDate" --components "Year,Month,Day,DayOfWeek" \
  --format "yyyy-MM-dd" --verbose
```

**Options:**
- `-i, --input` - Input file path (required)
- `-o, --output` - Output file path (required)
- `--column` - Source date column (required)
- `--components` - Components to extract: Year, Month, Day, Hour, Minute, Second, DayOfWeek (comma-separated, required)
- `--format` - Custom date format (optional)
- `--culture` - Culture for parsing (default: en-US)
- `--has-header` - Input has headers (default: true)
- `--verbose` - Show detailed progress

---

#### fill-missing
Fill missing or empty values in columns using various strategies.

**Syntax:**
```bash
fileprepper fill-missing -i INPUT -o OUTPUT --methods METHODS [OPTIONS]
```

**Example:**
```bash
# Fill Age with mean and City with fixed value
fileprepper fill-missing -i data.csv -o filled.csv \
  --methods "Age:Mean,City:FixedValue:Unknown" --verbose
```

**Options:**
- `-i, --input` - Input file path (required)
- `-o, --output` - Output file path (required)
- `--methods`, `-m` - Fill methods in format `Column:Method[:Value]` (comma-separated, required)
- `--append-to-source` - Append result to source file (default: false)
- `--output-column` - Output column template (optional)
- `--has-header` - Input has headers (default: true)
- `--verbose` - Show detailed progress

**Fill Methods:** Mean, Median, Mode, ForwardFill, BackwardFill, FixedValue

---

#### normalize
Normalize numeric columns to a specific range.

**Syntax:**
```bash
fileprepper normalize -i INPUT -o OUTPUT -c COLUMNS -m METHOD [OPTIONS]
```

**Example:**
```bash
# Normalize age and salary to 0-1 range
fileprepper normalize -i data.csv -o normalized.csv \
  -c "Age,Salary,Score" -m MinMax --min 0 --max 1 --verbose
```

**Options:**
- `-i, --input` - Input file path (required)
- `-o, --output` - Output file path (required)
- `-c, --columns` - Columns to normalize (comma-separated, required)
- `-m, --method` - Normalization method: MinMax, ZScore (required)
- `--min` - Min value for MinMax (default: 0)
- `--max` - Max value for MinMax (default: 1)
- `--has-header` - Input has headers (default: true)
- `--verbose` - Show detailed progress

---

#### scale
Scale numeric columns using statistical methods.

**Syntax:**
```bash
fileprepper scale -i INPUT -o OUTPUT -c COLUMNS -m METHOD [OPTIONS]
```

**Example:**
```bash
# Standardize height and weight
fileprepper scale -i data.csv -o scaled.csv \
  -c "Height,Weight,BMI" -m Standardization --verbose
```

**Options:**
- `-i, --input` - Input file path (required)
- `-o, --output` - Output file path (required)
- `-c, --columns` - Columns to scale (comma-separated, required)
- `-m, --method` - Scaling method: MinMax, Standardization (required)
- `--has-header` - Input has headers (default: true)
- `--verbose` - Show detailed progress

---

#### one-hot-encoding
Convert categorical variables to binary (0/1) columns.

**Syntax:**
```bash
fileprepper one-hot-encoding -i INPUT -o OUTPUT -c COLUMNS [OPTIONS]
```

**Example:**
```bash
# One-hot encode category and type columns
fileprepper one-hot-encoding -i data.csv -o encoded.csv \
  -c "Category,Type" --prefix "Cat_,Type_" --verbose
```

**Options:**
- `-i, --input` - Input file path (required)
- `-o, --output` - Output file path (required)
- `-c, --columns` - Categorical columns to encode (comma-separated, required)
- `--prefix` - Prefixes for encoded columns (comma-separated, optional)
- `--has-header` - Input has headers (default: true)
- `--verbose` - Show detailed progress

---

#### replace
Replace specific values in columns.

**Syntax:**
```bash
fileprepper replace -i INPUT -o OUTPUT --column COLUMN --old VALUES --new VALUE [OPTIONS]
```

**Example:**
```bash
# Replace N/A and Unknown with Pending
fileprepper replace -i data.csv -o replaced.csv \
  --column "Status" --old "N/A,Unknown,null" --new "Pending" --verbose
```

**Options:**
- `-i, --input` - Input file path (required)
- `-o, --output` - Output file path (required)
- `--column` - Target column (required)
- `--old` - Values to replace (comma-separated, required)
- `--new` - Replacement value (required)
- `--has-header` - Input has headers (default: true)
- `--verbose` - Show detailed progress

---

### Data Analysis

#### aggregate
Group data and perform aggregations.

**Syntax:**
```bash
fileprepper aggregate -i INPUT -o OUTPUT --group COLUMNS --aggregations FUNCS [OPTIONS]
```

**Example:**
```bash
# Group by category and region, calculate sum and average
fileprepper aggregate -i sales.csv -o aggregated.csv \
  --group "Category,Region" \
  --aggregations "Sales:Sum,Quantity:Avg,Orders:Count" --verbose
```

**Options:**
- `-i, --input` - Input file path (required)
- `-o, --output` - Output file path (required)
- `--group` - Grouping columns (comma-separated, required)
- `--aggregations` - Aggregations in format `Column:Function` (Sum/Avg/Min/Max/Count, comma-separated, required)
- `--has-header` - Input has headers (default: true)
- `--verbose` - Show detailed progress

---

#### stats
Calculate comprehensive statistics on numeric columns.

**Syntax:**
```bash
fileprepper stats -i INPUT -o OUTPUT -c COLUMNS --stats STATISTICS [OPTIONS]
```

**Example:**
```bash
# Calculate mean, min, max for age and salary
fileprepper stats -i employees.csv -o stats.csv \
  -c "Age,Salary,Score" --stats "Mean,Min,Max,Median,StandardDeviation" \
  --suffix "_stat" --verbose
```

**Options:**
- `-i, --input` - Input file path (required)
- `-o, --output` - Output file path (required)
- `-c, --columns` - Columns to analyze (comma-separated, required)
- `-s, --stats` - Statistics to calculate (comma-separated, required)
- `--suffix` - Suffix for output column names (default: "_stat")
- `--default-value` - Default value for calculation errors (optional)
- `--has-header` - Input has headers (default: true)
- `--verbose` - Show detailed progress

**Available Statistics:** Mean, StandardDeviation, Min, Max, Median, Q1, Q3, ZScore, RobustZScore, PercentRank, MAD

---

#### create-lag-features
Create lag features from time series data for machine learning.

**Syntax:**
```bash
fileprepper create-lag-features -i INPUT -o OUTPUT -g GROUP -t TIME -l COLUMNS -p PERIODS [OPTIONS]
```

**Example:**
```bash
# Create lag features for predictive modeling
fileprepper create-lag-features -i timeseries.csv -o features.csv \
  --group-by "PartNumber" --time-column "Date" \
  --lag-columns "Value,Temperature" --lag-periods "1,2,3" \
  --target "FailureStatus" --drop-nulls --verbose
```

**Options:**
- `-i, --input` - Input CSV file path (required)
- `-o, --output` - Output file path (required)
- `-g, --group-by` - Column to group by (e.g., Part Number, Entity ID) (required)
- `-t, --time-column` - Column representing time/sequence for sorting (required)
- `-l, --lag-columns` - Columns to create lag features from (comma-separated, required)
- `-p, --lag-periods` - Lag periods (e.g., 1,2,3) (comma-separated, required)
- `--target` - Target column to predict (optional, kept in output)
- `--drop-nulls` - Drop rows with null lag values (default: true)
- `-k, --keep-columns` - Additional columns to keep in output (comma-separated, optional)
- `--has-header` - Input has headers (default: true)
- `--verbose` - Show detailed progress

**Use Case:** Time series forecasting, predictive maintenance, sequential pattern analysis

---

### Data Organization

#### drop-duplicates
Remove duplicate rows based on specified columns.

**Syntax:**
```bash
fileprepper drop-duplicates -i INPUT -o OUTPUT -c COLUMNS [OPTIONS]
```

**Example:**
```bash
# Remove duplicates based on email
fileprepper drop-duplicates -i contacts.csv -o unique.csv \
  -c "Email" --keep First --verbose
```

**Options:**
- `-i, --input` - Input file path (required)
- `-o, --output` - Output file path (required)
- `-c, --columns` - Columns to check for duplicates (comma-separated, required)
- `--keep` - Which duplicate to keep: First or Last (default: First)
- `--has-header` - Input has headers (default: true)
- `--verbose` - Show detailed progress

---

#### filter-rows
Filter rows based on column conditions.

**Syntax:**
```bash
fileprepper filter-rows -i INPUT -o OUTPUT -c CONDITIONS [OPTIONS]
```

**Example:**
```bash
# Filter rows where Age > 30 AND Status is Active
fileprepper filter-rows -i data.csv -o filtered.csv \
  -c "Age:GreaterThan:30,Status:Equals:Active" --verbose
```

**Options:**
- `-i, --input` - Input file path (required)
- `-o, --output` - Output file path (required)
- `-c, --conditions` - Filter conditions in format `Column:Operator:Value` (comma-separated, required)
- `--has-header` - Input has headers (default: true)
- `--verbose` - Show detailed progress

**Operators:** Equals, NotEquals, GreaterThan, GreaterOrEqual, LessThan, LessOrEqual, Contains, NotContains, StartsWith, EndsWith

---

#### merge
Merge multiple files vertically (concatenate) or horizontally (join).

**Syntax:**
```bash
fileprepper merge -i FILE1 FILE2 [FILE3...] -o OUTPUT -t TYPE [OPTIONS]
```

**Examples:**
```bash
# Vertical merge (stack rows from multiple files)
fileprepper merge -i file1.csv file2.csv file3.csv -o merged.csv \
  --type Vertical --verbose

# Horizontal merge (join on ID column)
fileprepper merge -i users.csv salaries.csv -o combined.csv \
  --type Horizontal --join-type Inner --key-columns "ID" --verbose
```

**Options:**
- `-i, --input` - Input file paths (space-separated, required, minimum 2 files)
- `-o, --output` - Output file path (required)
- `-t, --type` - Merge type: Vertical (concatenate rows) or Horizontal (join columns) (required)
- `-j, --join-type` - Join type for horizontal merge: Inner, Left, Right, Full (default: Inner)
- `-k, --key-columns` - Key columns for horizontal merge (comma-separated, required for horizontal)
- `--has-header` - Input files have headers (default: true)
- `--verbose` - Show detailed progress

---

#### data-sampling
Sample data using various sampling methods.

**Syntax:**
```bash
fileprepper data-sampling -i INPUT -o OUTPUT -m METHOD [OPTIONS]
```

**Example:**
```bash
# Random sample 1000 rows
fileprepper data-sampling -i large.csv -o sample.csv \
  -m Random --size 1000 --verbose

# Stratified sampling by category
fileprepper data-sampling -i data.csv -o stratified.csv \
  -m Stratified --column "Category" --ratio 0.3 --verbose
```

**Options:**
- `-i, --input` - Input file path (required)
- `-o, --output` - Output file path (required)
- `-m, --method` - Sampling method: Random, Systematic, Stratified (required)
- `--size` - Sample size (number of rows, optional)
- `--ratio` - Sample ratio 0.0-1.0 (optional)
- `--column` - Stratification column (required for Stratified method)
- `--has-header` - Input has headers (default: true)
- `--verbose` - Show detailed progress

---

### File Format

#### convert-format
Convert between different file formats.

**Syntax:**
```bash
fileprepper convert-format -i INPUT -o OUTPUT -f FORMAT [OPTIONS]
```

**Examples:**
```bash
# Convert CSV to JSON
fileprepper convert-format -i data.csv -o data.json \
  -f JSON --pretty --verbose

# Convert CSV to XML
fileprepper convert-format -i data.csv -o data.xml \
  -f XML --pretty --verbose

# Convert Excel to CSV
fileprepper convert-format -i data.xlsx -o data.csv \
  -f CSV --verbose
```

**Options:**
- `-i, --input` - Input file path (required)
- `-o, --output` - Output file path (required)
- `-f, --format` - Target format: CSV, TSV, PSV, JSON, XML (required)
- `--pretty` - Pretty-print JSON/XML (default: false)
- `--has-header` - Input has headers (default: true)
- `--verbose` - Show detailed progress

---

## Advanced Usage

### Multi-Column Processing

Most commands support processing multiple columns simultaneously for efficiency:

```bash
# Normalize 5 columns at once
fileprepper normalize -i data.csv -o normalized.csv \
  -c "Age,Height,Weight,Salary,Score" -m MinMax --verbose

# Convert 4 types simultaneously
fileprepper convert-type -i data.csv -o typed.csv \
  -c "Date:DateTime:yyyy-MM-dd,Age:Integer,Active:Boolean,Salary:Decimal"

# Fill missing values in 3 columns with different strategies
fileprepper fill-missing -i data.csv -o filled.csv \
  --methods "Age:Mean,City:Mode,Score:Median"
```

### Error Handling

```bash
# Ignore errors and continue processing
fileprepper normalize -i dirty.csv -o normalized.csv \
  -c "Age,Salary" -m MinMax --ignore-errors --verbose

# Use default value for calculation errors
fileprepper stats -i data.csv -o stats.csv \
  -c "Value" --stats "Mean" --default-value "0" --ignore-errors
```

### Pipeline Workflows

Chain multiple commands for complex data transformations:

```bash
# Complete data preparation pipeline
# Step 1: Clean missing values
fileprepper fill-missing -i raw.csv -o step1.csv \
  --methods "Age:Mean,City:Mode" --verbose

# Step 2: Filter valid data
fileprepper filter-rows -i step1.csv -o step2.csv \
  -c "Age:GreaterThan:0,Age:LessThan:120" --verbose

# Step 3: Normalize numeric columns
fileprepper normalize -i step2.csv -o step3.csv \
  -c "Age,Salary,Score" -m MinMax --verbose

# Step 4: Create lag features for time series
fileprepper create-lag-features -i step3.csv -o step4.csv \
  --group-by "ID" --time-column "Date" \
  --lag-columns "Score" --lag-periods "1,2,3" --verbose

# Step 5: Convert to JSON format
fileprepper convert-format -i step4.csv -o final.json \
  -f JSON --pretty --verbose

# Cleanup intermediate files
rm step1.csv step2.csv step3.csv step4.csv
```

### Batch Processing with Shell Scripts

Process multiple files with loops:

```bash
# Process all CSV files in a directory
for file in data/*.csv; do
    output="processed/$(basename "$file")"
    fileprepper normalize -i "$file" -o "$output" -c "Age,Salary" -m MinMax
done

# Apply same transformation to multiple files
files="jan.csv feb.csv mar.csv"
for file in $files; do
    fileprepper filter-rows -i "$file" -o "filtered_$file" \
      -c "Status:Equals:Active"
done
```

---

## Tips & Best Practices

1. **Test with small files first** - Verify commands work correctly on sample data before processing large datasets
2. **Use --help extensively** - Every command has detailed built-in help: `fileprepper <command> --help`
3. **Check headers** - Ensure `--has-header` matches your data (default is true)
4. **Enable verbose mode** - Use `--verbose` to see detailed progress and validation results
5. **Error handling** - Use `--ignore-errors` for dirty data that may have parsing issues
6. **Backup originals** - Always keep copies of original data before transforming
7. **Watch column names** - Column names are case-sensitive and must match exactly
8. **Quote arguments** - Use quotes for arguments with spaces or special characters
9. **Pipeline incrementally** - Build complex workflows step-by-step, validating each stage

---

## Troubleshooting

### Installation Issues

```bash
# Install as global .NET tool
dotnet tool install -g fileprepper-cli

# Update if already installed
dotnet tool update -g fileprepper-cli

# Uninstall and reinstall
dotnet tool uninstall -g fileprepper-cli
dotnet tool install -g fileprepper-cli

# Run from source for development
cd src/FilePrepper.CLI
dotnet run -- <command> [options]
```

### Command Not Found

- Ensure .NET global tools path is in your PATH environment variable
- On Windows: `%USERPROFILE%\.dotnet\tools`
- On Linux/Mac: `~/.dotnet/tools`

### Invalid Column Names

- Check for typos - column names are case-sensitive
- Use exact names from CSV headers
- Quote column names with spaces: `--columns "First Name,Last Name"`
- Verify the file has headers if using `--has-header true`

### File Format Issues

- Check file encoding (UTF-8 recommended)
- Verify CSV delimiter matches file (comma for CSV, tab for TSV)
- Look for malformed rows or inconsistent column counts
- Use `--ignore-errors` to skip problematic rows

### Performance Issues

- Large files (>1GB) may need increased memory
- Consider splitting large files into smaller batches
- Use appropriate data types (Integer vs Decimal) for better performance
- Close other applications to free up system resources
- For very large datasets, consider using a database or big data tools

### Common Error Messages

**"Input file not found"**
- Verify file path is correct (use absolute paths or correct relative paths)
- Check file exists: `ls <file>` (Linux/Mac) or `dir <file>` (Windows)

**"Invalid argument format"**
- Check command syntax with `--help`
- Ensure comma-separated lists have no spaces (or quote the entire argument)
- Verify operators and method names are spelled correctly

**"Column not found"**
- Verify column name matches exactly (case-sensitive)
- Check file has headers if using `--has-header true`
- List headers: `head -1 file.csv`

**"Failed to parse value"**
- Data may not match expected type (e.g., text in numeric column)
- Use `--ignore-errors` to skip invalid values
- Specify `--default-value` for error cases

---

## Getting Help

- **Built-in help**: `fileprepper --help` or `fileprepper <command> --help`
- **Documentation**: https://github.com/iyulab-rnd/FilePrepper
- **Issues**: https://github.com/iyulab-rnd/FilePrepper/issues
- **Examples**: See `docs/Common-Scenarios.md` for real-world examples

---

## Version History

### v0.4.0 (Latest)
- System.CommandLine integration for robust CLI framework
- Spectre.Console rich terminal UI with colors and progress
- Enhanced validation and error messages
- Improved help documentation
- All 20 commands with consistent syntax

### v0.3.x
- Initial CLI implementation
- Core data processing commands
- Basic file format support
