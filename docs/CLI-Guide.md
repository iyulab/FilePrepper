# CLI Reference Guide

Every command and option below is read from the tool itself (`fileprepper <command> --help`); the tests check the documents against the same command tree.

## General Syntax

```bash
fileprepper <command> [options]

# Get help
fileprepper --help
fileprepper <command> --help
```

## Common Options

Every command takes these:

| Option | Aliases | Description | Default |
|--------|---------|-------------|---------|
| `--has-header` | `--header` | Whether input files have headers | true |
| `--ignore-errors` | - | Whether to ignore errors during processing | false |
| `--verbose` | `-v` | Enable verbose output | false |
| `--encoding` | - | File encoding (auto, utf-8, cp949, euc-kr) | auto |
| `--skip-rows` | - | Number of rows to skip before header | 0 |
| `--quiet` | `-q` | Suppress non-error output | false |

Input and output are `-i, --input` and `-o, --output` on every command (`merge` and `merge-asof` take several input files).

## Supported File Formats

- **CSV** (.csv) - Comma-Separated Values
- **TSV** (.tsv) - Tab-Separated Values
- **JSON** (.json) - JavaScript Object Notation
- **XML** (.xml) - Extensible Markup Language
- **Excel** (.xlsx, .xls) - Microsoft Excel Spreadsheet
- **Parquet** (.parquet) - read

---

## Commands

### Data Transformation

#### normalize

Normalize numeric columns

```bash
fileprepper normalize --input <input> --output <output> --columns <columns> --method <method> [options]
```

| Option | Description |
|--------|-------------|
| `-c`, `--columns` `<columns>` | Columns to normalize (required) |
| `-m`, `--method` `<method>` | Normalization method (MinMax/ZScore) (required) |
| `--min` `<min>` | Min value for MinMax scaling [default: 0] |
| `--max` `<max>` | Max value for MinMax scaling [default: 1] |

#### scale

Scale numeric columns using specified methods

```bash
fileprepper scale --input <input> --output <output> --scaling <scaling> [options]
```

| Option | Description |
|--------|-------------|
| `-s`, `--scaling` `<scaling>` | Scaling methods in format column:method (e.g. Price:MinMax,Score:Standardization) (required) |

#### one-hot-encoding

Perform one-hot encoding on categorical columns

```bash
fileprepper one-hot-encoding --input <input> --output <output> --columns <columns> [options]
```

| Option | Description |
|--------|-------------|
| `-c`, `--columns` `<columns>` | Columns to encode (required) |
| `--drop-first` | Drop first category |
| `--keep-original` | Keep original columns |

#### convert-type

Convert data types of columns

```bash
fileprepper convert-type --input <input> --output <output> --conversions <conversions> [options]
```

| Option | Description |
|--------|-------------|
| `-c`, `--conversions` `<conversions>` | Type conversions in format column:type[:format] (e.g. Date:DateTime:yyyy-MM-dd) (required) |
| `--culture` `<culture>` | Culture for parsing (e.g. en-US, ko-KR) [default: en-US] |

#### extract-date

Extract components from date columns

```bash
fileprepper extract-date --input <input> --output <output> --extractions <extractions> [options]
```

| Option | Description |
|--------|-------------|
| `-e`, `--extractions` `<extractions>` | Date extractions in format column:component1,component2[:format]; separate several extractions with ';' (e.g. OrderDate:Year,Month;ShipDate:Year) (required) |
| `--culture` `<culture>` | Culture for parsing dates [default: en-US] |
| `--append-to-source` | Append to source file |
| `--output-column` `<output-column>` | Output column template |

#### datetime

Parse DateTime and extract features

```bash
fileprepper datetime --input <input> --output <output> --column <column> [options]
```

| Option | Description |
|--------|-------------|
| `-c`, `--column` `<column>` | Column to parse/transform (required) |
| `-m`, `--mode` `<mode>` | Operation mode: parse, excel, features [default: parse] |
| `-f`, `--format` `<format>` | Input format for parse mode (e.g., 'yyyy-MM-dd HH:mm', 'yyyyMMddHHmm') |
| `-of`, `--output-format` `<output-format>` | Output format [default: yyyy-MM-dd HH:mm:ss] |
| `-ft`, `--features` `<features>` | Features to extract (comma-separated): Year,Month,Day,Hour,Minute,DayOfWeek,DayOfYear,WeekOfYear,Quarter |
| `--remove-original` | Remove original column after feature extraction |

#### string

String operations (substring, concat, replace, trim, upper, lower)

```bash
fileprepper string --input <input> --output <output> [options]
```

| Option | Description |
|--------|-------------|
| `-m`, `--mode` `<mode>` | Operation mode: substring, concat, replace, trim, upper, lower [default: trim] |
| `-c`, `--column` `<column>` | Column to operate on |
| `-oc`, `--output-column` `<output-column>` | Output column name (default: same as input or auto-generated) |
| `-s`, `--start-index` `<start-index>` | Start index for substring (0-based) [default: 0] |
| `-l`, `--length` `<length>` | Length for substring (default: to end) |
| `-cols`, `--columns` `<columns>` | Columns to concatenate (comma-separated) |
| `-sep`, `--separator` `<separator>` | Separator for concat |
| `-old`, `--old-value` `<old-value>` | Value to replace |
| `-new`, `--new-value` `<new-value>` | Replacement value |
| `-tm`, `--trim-mode` `<trim-mode>` | Trim mode: both, start, end [default: both] |

#### conditional

Create a new column based on conditional logic (if-then-else)

```bash
fileprepper conditional --input <input> --output <output> --output-column <output-column> --conditions <conditions> [options]
```

| Option | Description |
|--------|-------------|
| `-oc`, `--output-column` `<output-column>` | Name of the new column to create (required) |
| `-c`, `--conditions` `<conditions>` | Condition-value pairs (format: 'Column operator Value : ResultValue'). Example: 'Price > 100 : High'. Multiple conditions evaluated in order (first match wins). (required) |
| `-e`, `--else` `<else>` | Default value if no conditions match (else value) |

#### expression

Create computed columns from arithmetic expressions

```bash
fileprepper expression --input <input> --output <output> --expressions <expressions> [options]
```

| Option | Description |
|--------|-------------|
| `-e`, `--expressions` `<expressions>` | Column expressions (format: 'output=expression' or 'output=expression@position', space-separated) (required) |
| `-r`, `--remove-source` | Remove source columns used in expressions |

### Data Cleaning

#### fill-missing

Fill missing values in columns

```bash
fileprepper fill-missing --input <input> --output <output> --methods <methods> [options]
```

| Option | Description |
|--------|-------------|
| `-m`, `--methods` `<methods>` | Fill methods in format column:method[:value] (e.g. Age:Mean or Score:FixedValue:0) (required) |
| `--append-to-source` | Append to source |
| `--output-column` `<output-column>` | Output column template |

#### drop-duplicates

Remove duplicate rows

```bash
fileprepper drop-duplicates --input <input> --output <output> [options]
```

| Option | Description |
|--------|-------------|
| `--keep-first` | Keep first occurrence of duplicates |
| `--subset-only` | Check duplicates only on specified columns |
| `-c`, `--columns` `<columns>` | Columns to check for duplicates |

#### replace

Replace values in specified columns

```bash
fileprepper replace --input <input> --output <output> --replacements <replacements> [options]
```

| Option | Description |
|--------|-------------|
| `-r`, `--replacements` `<replacements>` | Replacement rules in format column:oldValue=newValue[;oldValue2=newValue2] (required) |

#### remove-constants

Remove constant or near-constant columns from the input file

```bash
fileprepper remove-constants --input <input> --output <output> [options]
```

| Option | Description |
|--------|-------------|
| `-t`, `--threshold` `<threshold>` | Unique ratio threshold (0.0 = exact constants only, 0.01 = below 1% unique ratio) [default: 0] |
| `--report-only` | Only report constant columns without removing them |

#### clean

Clean CSV numeric data (remove thousand separators)

```bash
fileprepper clean --input <input> --output <output> [options]
```

| Option | Description |
|--------|-------------|
| `-c`, `--columns` `<columns>` | Target columns to clean (if empty, all columns are cleaned, space-separated) |
| `-s`, `--separator` `<separator>` | Thousand separator character to remove [default: ,] |
| `-val`, `--validate` | Validate that cleaned values are valid numbers |
| `--strip-cr` | Strip carriage return (\r) characters from field values |

### Column Operations

#### add-columns

Add new columns to the CSV file with specified values

```bash
fileprepper add-columns --input <input> --output <output> --columns <columns> [options]
```

| Option | Description |
|--------|-------------|
| `-c`, `--columns` `<columns>` | Columns to add in format name=value (e.g. Age=30,City=Seoul) (required) |

#### remove-columns

Remove specified columns from the input file

```bash
fileprepper remove-columns --input <input> --output <output> --columns <columns> [options]
```

| Option | Description |
|--------|-------------|
| `-c`, `--columns` `<columns>` | Columns to remove (required) |

#### rename-columns

Rename columns in the input file

```bash
fileprepper rename-columns --input <input> --output <output> --mappings <mappings> [options]
```

| Option | Description |
|--------|-------------|
| `-m`, `--mappings` `<mappings>` | Column rename mappings in format oldName:newName (required) |

#### reorder-columns

Reorder columns in the specified order

```bash
fileprepper reorder-columns --input <input> --output <output> --order <order> [options]
```

| Option | Description |
|--------|-------------|
| `--order` `<order>` | Desired column order (required) |

#### column-interaction

Perform operations between columns

```bash
fileprepper column-interaction --input <input> --output <output> --source <source> --type <type> --column <column> [options]
```

| Option | Description |
|--------|-------------|
| `-s`, `--source` `<source>` | Source columns (required) |
| `-t`, `--type` `<type>` | Operation type (Add/Subtract/Multiply/Divide/Concat/Custom) (required) |
| `-c`, `--column` `<column>` | Output column name (required) |
| `-e`, `--expression` `<expression>` | Custom expression (use $1, $2, etc.) |
| `--default-value` `<default-value>` | Default value for errors |

### Data Organization

#### merge

Merge multiple files vertically (concatenate) or horizontally (join)

```bash
fileprepper merge --output <output> --type <type> [options]
```

| Option | Description |
|--------|-------------|
| `-p`, `--input-pattern` `<input-pattern>` | Glob pattern for input files (e.g., 'data/*.csv') |
| `-t`, `--type` `<type>` | Merge type: Vertical (concatenate rows) or Horizontal (join columns) (required) |
| `-j`, `--join-type` `<join-type>` | Join type for horizontal merge: Inner, Left, Right, Full [default: Inner] |
| `-k`, `--key-columns` `<key-columns>` | Key columns for horizontal merge (comma-separated) |
| `-m`, `--join-mappings` `<join-mappings>` | Join column mappings for heterogeneous column names (format: 'left:right' or 'left:right:output', space-separated) |

#### merge-asof

Merge two files based on nearest time matching (merge_asof)

```bash
fileprepper merge-asof --input <input> --output <output> --left-on <left-on> --right-on <right-on> [options]
```

| Option | Description |
|--------|-------------|
| `-l`, `--left-on` `<left-on>` | Column name in left file to use for matching (typically datetime) (required) |
| `-r`, `--right-on` `<right-on>` | Column name in right file to use for matching (typically datetime) (required) |
| `-d`, `--direction` `<direction>` | Matching direction: Backward (most recent <=), Forward (nearest >=), or Nearest (closest) [default: Backward] |
| `-t`, `--tolerance` `<tolerance>` | Maximum time difference allowed for matching (in seconds). If not specified, no tolerance limit is applied. |
| `-s`, `--suffix` `<suffix>` | Suffix to add to right file columns to avoid name conflicts [default: _right] |

#### data-sampling

Sample data from the input file

```bash
fileprepper data-sampling --input <input> --output <output> --method <method> --size <size> [options]
```

| Option | Description |
|--------|-------------|
| `-m`, `--method` `<method>` | Sampling method (Random/Systematic/Stratified) (required) |
| `-s`, `--size` `<size>` | Sample size (ratio 0-1 or absolute count) (required) |
| `--seed` `<seed>` | Random seed for reproducibility |
| `--stratify` `<stratify>` | Column for stratified sampling |
| `--interval` `<interval>` | Interval for systematic sampling |

#### convert-format

Convert file format

```bash
fileprepper convert-format --input <input> --output <output> --target <target> [options]
```

| Option | Description |
|--------|-------------|
| `-t`, `--target` `<target>` | Target format (CSV/TSV/PSV/JSON/XML) (required) |
| `-e`, `--output-encoding` `<output-encoding>` | Output file encoding [default: utf-8] |
| `--pretty` | Pretty print for JSON/XML |
| `--root` `<root>` | Root element name for XML [default: root] |
| `--item` `<item>` | Item element name for XML [default: item] |

#### unpivot

Transform wide format data to long format (unpivot)

```bash
fileprepper unpivot --input <input> --output <output> --column-groups <column-groups> --value-columns <value-columns> [options]
```

| Option | Description |
|--------|-------------|
| `-b`, `--base-columns` `<base-columns>` | Base columns to keep in every output row (space-separated) |
| `-g`, `--column-groups` `<column-groups>` | Column groups to unpivot (space-separated) (required) |
| `-idx`, `--index-column` `<index-column>` | Name for the index column in output [default: Index] |
| `-vc`, `--value-columns` `<value-columns>` | Names for value columns in output (space-separated) (required) |
| `-se`, `--skip-empty` | Skip rows where all value columns are empty |

#### explode

Turn list columns (a JSON list per cell) into one row per element

```bash
fileprepper explode --input <input> --output <output> --columns <columns> [options]
```

| Option | Description |
|--------|-------------|
| `-c`, `--columns` `<columns>` | List columns to explode together (comma-separated); in each row their lists must have the same length (required) |

#### split-list

Split list columns of one length (a JSON list per cell) into one column per position: name.0, name.1, …

```bash
fileprepper split-list --input <input> --output <output> --columns <columns> [options]
```

| Option | Description |
|--------|-------------|
| `-c`, `--columns` `<columns>` | List columns to split (comma-separated); every list of a column must have the same length (required) |

#### filter-rows

Filter rows based on column conditions (e.g., Age:GreaterThan:30)

```bash
fileprepper filter-rows --input <input> --output <output> --conditions <conditions> [options]
```

| Option | Description |
|--------|-------------|
| `-c`, `--conditions` `<conditions>` | Filter conditions in format column:operator:value (e.g., Age:GreaterThan:30). Operators: Equals, NotEquals, GreaterThan, GreaterOrEqual, LessThan, LessOrEqual, Contains, NotContains, StartsWith, EndsWith (required) |

### Data Analysis

#### stats

Calculate basic statistics on numeric columns

```bash
fileprepper stats --input <input> --output <output> --columns <columns> --stats <stats> [options]
```

| Option | Description |
|--------|-------------|
| `-c`, `--columns` `<columns>` | Target columns (required) |
| `-s`, `--stats` `<stats>` | Statistics to calculate (Mean/StandardDeviation/Min/Max/Median/Q1/Q3/ZScore/RobustZScore/PercentRank/MAD) (required) |
| `--suffix` `<suffix>` | Suffix for output column names [default: _stat] |
| `--default-value` `<default-value>` | Default value for errors |

#### aggregate

Aggregate data based on group by columns

```bash
fileprepper aggregate --input <input> --output <output> --group-by <group-by> --aggregates <aggregates> [options]
```

| Option | Description |
|--------|-------------|
| `-g`, `--group-by` `<group-by>` | Columns to group by (required) |
| `-a`, `--aggregates` `<aggregates>` | Aggregate functions in format column:function:output (e.g. Sales:Sum:TotalSales). Functions: Sum, Average, Count, Min, Max (required) |
| `--append-to-source` | Append result to source file |
| `--output-column` `<output-column>` | Output column name template when appending |

### Feature Engineering

#### create-lag-features

Create lag features from time series data for machine learning

```bash
fileprepper create-lag-features --input <input> --output <output> --group-by <group-by> --time-column <time-column> --lag-columns <lag-columns> --lag-periods <lag-periods> [options]
```

| Option | Description |
|--------|-------------|
| `-g`, `--group-by` `<group-by>` | Column to group by (e.g., Part Number, Entity ID) (required) |
| `-t`, `--time-column` `<time-column>` | Column representing time/sequence for sorting within groups (required) |
| `-l`, `--lag-columns` `<lag-columns>` | Comma-separated list of columns to create lag features from (required) |
| `-p`, `--lag-periods` `<lag-periods>` | Comma-separated list of lag periods (e.g., 1,2,3) (required) |
| `--target` `<target>` | Target column to predict (optional, will be kept in output) |
| `--drop-nulls` | Drop rows with null lag values |
| `-k`, `--keep-columns` `<keep-columns>` | Comma-separated list of additional columns to keep in output |

#### window

Apply window operations (resample or rolling aggregations)

```bash
fileprepper window --input <input> --output <output> --type <type> --columns <columns> [options]
```

| Option | Description |
|--------|-------------|
| `-t`, `--type` `<type>` | Window type: resample (time-based) or rolling (row-based) (required) |
| `-m`, `--method` `<method>` | Aggregation method: mean, min, max, sum, count, std [default: mean] |
| `-c`, `--columns` `<columns>` | Target columns to aggregate (comma-separated) (required) |
| `-tc`, `--time-column` `<time-column>` | Time column for resample (required for resample type) |
| `-w`, `--window` `<window>` | Window size for resample: e.g., '5T' (5 minutes), '1H' (1 hour), '1D' (1 day) |
| `-ws`, `--window-size` `<window-size>` | Window size in rows for rolling aggregation [default: 3] |
| `-s`, `--suffix` `<suffix>` | Suffix for rolling aggregation output columns [default: _rolling] |

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
  --target JSON --pretty --verbose

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
- **Documentation**: https://github.com/iyulab/FilePrepper
- **Issues**: https://github.com/iyulab/FilePrepper/issues
- **Examples**: See `docs/Common-Scenarios.md` for real-world examples

---
