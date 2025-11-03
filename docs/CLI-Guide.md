# CLI Reference Guide

Complete command-line reference for FilePrepper.

## General Syntax

```bash
fileprepper <command> [options]

# Get help
fileprepper --help
fileprepper <command> --help
```

## Common Options

Available for all commands:

| Option | Description | Default |
|--------|-------------|---------|
| `-i, --input` | Input file path | Required |
| `-o, --output` | Output file path | Required |
| `--has-header` | Input has header row | true |
| `--ignore-errors` | Continue on errors | false |
| `--default-value` | Value for errors | - |

## Commands

### Data Manipulation

#### add-columns
Add new columns with constant values.

```bash
fileprepper add-columns -i input.csv -o output.csv --columns "Status:Active,Priority:High"
```

**Options:**
- `--columns`: Column definitions in format `Name:Value` (comma-separated)

#### remove-columns
Remove specified columns.

```bash
fileprepper remove-columns -i input.csv -o output.csv -c "Temporary,Notes,Debug"
```

**Options:**
- `-c, --columns`: Columns to remove (comma-separated)

#### rename-columns
Rename columns using mapping.

```bash
fileprepper rename-columns -i input.csv -o output.csv --map "OldName:NewName,Age:Years"
```

**Options:**
- `--map`: Rename mappings in format `Old:New` (comma-separated)

#### reorder-columns
Change column order.

```bash
fileprepper reorder-columns -i input.csv -o output.csv -c "ID,Name,Email,Age"
```

**Options:**
- `-c, --columns`: Desired column order (comma-separated)

#### column-interaction
Perform operations between columns.

```bash
fileprepper column-interaction -i input.csv -o output.csv \
  --source "Price,Quantity" --operation Multiply --output "Total"
```

**Options:**
- `--source`: Source columns (comma-separated)
- `--operation`: Operation (Add/Subtract/Multiply/Divide/Concatenate)
- `--output`: Output column name

### Data Transformation

#### convert-type
Convert column data types.

```bash
fileprepper convert-type -i input.csv -o output.csv \
  -c "Date:DateTime:yyyy-MM-dd,Age:Integer,Price:Decimal,Active:Boolean"
```

**Options:**
- `-c, --conversions`: Conversions in format `Column:Type[:Format]`
- `--culture`: Culture for parsing (default: en-US)

**Supported Types:** String, Integer, Decimal, DateTime, Boolean

#### extract-date
Extract components from date columns.

```bash
fileprepper extract-date -i input.csv -o output.csv \
  --column "OrderDate" --components "Year,Month,Day,DayOfWeek"
```

**Options:**
- `--column`: Source date column
- `--components`: Components to extract (Year/Month/Day/Hour/Minute/Second/DayOfWeek/etc.)
- `--format`: Custom date format
- `--culture`: Culture for parsing

#### fill-missing
Fill missing values.

```bash
fileprepper fill-missing -i input.csv -o output.csv \
  -c "Age,Salary" -m Mean
```

**Options:**
- `-c, --columns`: Columns to process (comma-separated)
- `-m, --method`: Fill method (Mean/Median/Mode/Forward/Backward/Constant)
- `--value`: Value for Constant method

#### normalize
Normalize numeric columns.

```bash
fileprepper normalize -i input.csv -o output.csv \
  -c "Age,Salary,Score" -m MinMax --min 0 --max 1
```

**Options:**
- `-c, --columns`: Columns to normalize (comma-separated)
- `-m, --method`: Normalization method (MinMax/ZScore)
- `--min`: Min value for MinMax (default: 0)
- `--max`: Max value for MinMax (default: 1)

#### scale
Scale numeric columns.

```bash
fileprepper scale -i input.csv -o output.csv \
  -c "Height,Weight" -m Standardization
```

**Options:**
- `-c, --columns`: Columns to scale (comma-separated)
- `-m, --method`: Scaling method (MinMax/Standardization)

#### one-hot-encoding
Convert categorical variables to binary columns.

```bash
fileprepper one-hot-encoding -i input.csv -o output.csv \
  -c "Category,Type" --prefix "Cat_,Type_"
```

**Options:**
- `-c, --columns`: Categorical columns (comma-separated)
- `--prefix`: Prefixes for encoded columns (comma-separated)

#### replace
Replace values in columns.

```bash
fileprepper replace -i input.csv -o output.csv \
  --column "Status" --old "N/A,Unknown" --new "Pending"
```

**Options:**
- `--column`: Target column
- `--old`: Values to replace (comma-separated)
- `--new`: Replacement value

### Data Analysis

#### aggregate
Group and aggregate data.

```bash
fileprepper aggregate -i input.csv -o output.csv \
  --group "Category,Region" \
  --aggregations "Sales:Sum,Quantity:Avg,Orders:Count"
```

**Options:**
- `--group`: Grouping columns (comma-separated)
- `--aggregations`: Aggregations in format `Column:Function` (Sum/Avg/Min/Max/Count)

#### stats
Calculate statistics.

```bash
fileprepper stats -i input.csv -o output.csv \
  -c "Age,Salary,Score"
```

**Options:**
- `-c, --columns`: Columns to analyze (comma-separated)

**Output includes:** Count, Mean, StdDev, Min, Q1, Median, Q3, Max

### Data Organization

#### drop-duplicates
Remove duplicate rows.

```bash
fileprepper drop-duplicates -i input.csv -o output.csv \
  -c "Email,PhoneNumber" --keep First
```

**Options:**
- `-c, --columns`: Columns to check for duplicates (comma-separated)
- `--keep`: Which duplicate to keep (First/Last)

#### filter-rows
Filter rows by conditions.

```bash
fileprepper filter-rows -i input.csv -o output.csv \
  --column "Age" --operator GreaterThan --value "18"
```

**Options:**
- `--column`: Column to filter
- `--operator`: Comparison (Equals/NotEquals/GreaterThan/LessThan/Contains/StartsWith/EndsWith)
- `--value`: Comparison value

#### merge
Merge multiple files.

```bash
# Vertical merge (stack rows)
fileprepper merge file1.csv file2.csv file3.csv -o merged.csv -t Vertical

# Horizontal merge (join columns)
fileprepper merge file1.csv file2.csv -o merged.csv \
  -t Horizontal --join Inner --key "ID"
```

**Options:**
- `-t, --type`: Merge type (Vertical/Horizontal)
- `--join`: Join type for horizontal (Inner/Left/Right/Outer)
- `--key`: Join key column

#### data-sampling
Sample data.

```bash
fileprepper data-sampling -i input.csv -o output.csv \
  -m Random --size 1000
```

**Options:**
- `-m, --method`: Sampling method (Random/Systematic/Stratified)
- `--size`: Sample size (rows)
- `--ratio`: Sample ratio (0.0-1.0)
- `--column`: Stratification column (for Stratified)

### File Format

#### convert-format
Convert file formats.

```bash
fileprepper convert-format -i input.csv -o output.json -f JSON
fileprepper convert-format -i input.csv -o output.xml -f XML
```

**Options:**
- `-f, --format`: Target format (CSV/TSV/PSV/JSON/XML)
- `--pretty`: Pretty-print JSON/XML

## Advanced Usage

### Multi-Column Processing

Most commands support processing multiple columns simultaneously:

```bash
# Process 5 columns at once
fileprepper normalize -i data.csv -o output.csv \
  -c "Col1,Col2,Col3,Col4,Col5" -m MinMax

# Convert 3 types simultaneously
fileprepper convert-type -i data.csv -o output.csv \
  -c "Date:DateTime,Age:Integer,Active:Boolean"
```

### Error Handling

```bash
# Ignore errors and use default value
fileprepper normalize -i data.csv -o output.csv \
  -c "Age,Salary" -m MinMax \
  --ignore-errors --default-value "0"
```

### Pipeline Workflows

Chain multiple commands for complex workflows:

```bash
# Step 1: Clean data
fileprepper fill-missing -i raw.csv -o step1.csv -c "Age" -m Mean

# Step 2: Normalize
fileprepper normalize -i step1.csv -o step2.csv -c "Age,Salary" -m MinMax

# Step 3: Convert format
fileprepper convert-format -i step2.csv -o final.json -f JSON

# Cleanup
rm step1.csv step2.csv
```

## Tips

1. **Test with small files first** - Verify commands on sample data
2. **Use --help** - Every command has detailed help
3. **Check headers** - Use `--has-header true/false` correctly
4. **Error handling** - Use `--ignore-errors` for dirty data
5. **Backup originals** - Keep copies before transforming

## Troubleshooting

### Command not found
```bash
# If installed as global tool
dotnet tool install -g FilePrepper.CLI

# Update if already installed
dotnet tool update -g FilePrepper.CLI

# Or run from source
cd src/FilePrepper.CLI
dotnet run -- <command> [options]
```

### Invalid column names
- Check for spaces in column names: use quotes
- Verify column names match CSV headers exactly

### Performance issues
- Large files may need more memory
- Consider splitting into smaller batches
- Use appropriate data types (Integer vs Decimal)
