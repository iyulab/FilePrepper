# Common Scenarios

Real-world examples for typical data processing tasks.

## Data Cleaning

### Remove Junk Columns and Duplicates

```bash
# Remove debugging/temporary columns
fileprepper remove-columns -i raw_data.csv -o step1.csv \
  -c "Debug,TempCol,Notes,Internal_ID"

# Remove duplicate records
fileprepper drop-duplicates -i step1.csv -o clean_data.csv \
  -c "Email,PhoneNumber" --subset-only --keep-first
```

### Handle Missing Data

```bash
# Fill numeric columns with mean
fileprepper fill-missing -i data.csv -o filled.csv \
  -m "Age:Mean,Salary:Mean,Score:Mean"

# Fill categorical with mode
fileprepper fill-missing -i data.csv -o filled.csv \
  -m "Category:Mode,Status:Mode" --ignore-errors

# Forward fill time series
fileprepper fill-missing -i timeseries.csv -o filled.csv \
  -m "Value:ForwardFill"
```

### Fix Data Types

```bash
# Convert imported data to proper types
fileprepper convert-type -i import.csv -o typed.csv \
  -c "OrderDate:DateTime:yyyy-MM-dd,Quantity:Integer,Price:Decimal,IsActive:Boolean" \
  --culture en-US
```

## Machine Learning Prep

### Feature Engineering

```bash
# Extract date features
fileprepper extract-date -i orders.csv -o features1.csv \
  --extractions "OrderDate:Year,Month,DayOfWeek,Hour"

# Create interaction features
fileprepper column-interaction -i features1.csv -o features2.csv \
  --source "Price,Quantity" --type Multiply --column "Revenue"

# Create lag features for time series
fileprepper create-lag-features -i timeseries.csv -o lagged.csv \
  --group-by "Store" --time-column "Date" \
  --lag-columns "Sales" --lag-periods "1,7,30"
```

### Normalize and Scale

```bash
# Normalize features for neural networks (0-1 range)
fileprepper normalize -i features.csv -o normalized.csv \
  -c "Age,Income,Score,Rating,Distance" \
  -m MinMax --min 0 --max 1

# Standardize for linear models (mean=0, std=1)
fileprepper scale -i features.csv -o scaled.csv \
  -s "Height:Standardization,Weight:Standardization,BMI:Standardization,BloodPressure:Standardization"
```

### Encode Categorical Variables

```bash
# One-hot encode categories
fileprepper one-hot-encoding -i data.csv -o encoded.csv \
  -c "Category,Region,Status"

# Result: Category_A, Category_B, Region_East, Region_West, etc.
```

### Train/Test Split

```bash
# 80% training sample
fileprepper data-sampling -i data.csv -o train.csv \
  -m Random -s 0.8

# Stratified sampling by target class
fileprepper data-sampling -i data.csv -o balanced.csv \
  -m Stratified --stratify "TargetClass" -s 1000
```

## Business Analytics

### Sales Data Aggregation

```bash
# Daily sales summary by region
fileprepper aggregate -i sales.csv -o daily_summary.csv \
  --group-by "Date,Region" \
  --aggregates "Revenue:Sum:TotalRevenue,Orders:Count:OrderCount,ItemsSold:Sum:TotalItems,Price:Average:AvgPrice"

# Product performance
fileprepper aggregate -i sales.csv -o products.csv \
  --group-by "ProductID,Category" \
  --aggregates "Revenue:Sum:TotalRevenue,Quantity:Sum:TotalQuantity,Price:Average:AvgPrice"
```

### Customer Analytics

```bash
# Calculate customer metrics
fileprepper aggregate -i transactions.csv -o customers.csv \
  --group-by "CustomerID" \
  --aggregates "TransactionAmount:Sum:TotalSpent,TransactionDate:Count:Transactions,TransactionAmount:Average:AvgSpent"

# RFM analysis prep
fileprepper aggregate -i orders.csv -o rfm.csv \
  --group-by "CustomerID" \
  --aggregates "OrderDate:Max:LastOrder,OrderValue:Sum:TotalValue,OrderID:Count:Orders"
```

### Report Generation

```bash
# Create summary statistics
fileprepper stats -i sales_data.csv -o statistics.csv \
  -c "Revenue,Profit,Units,CustomerCount" -s Mean,Median,StandardDeviation

# Export to JSON for API
fileprepper convert-format -i report.csv -o report.json \
  -t JSON --pretty
```

## Data Integration

### Merge Multiple Sources

```bash
# Combine monthly files vertically
fileprepper merge -i jan.csv feb.csv mar.csv -o q1.csv -t Vertical

# Join customer and order data
fileprepper merge -i customers.csv orders.csv -o enriched.csv \
  -t Horizontal --join-type Left --key-columns "CustomerID"

# Join multiple dimension tables
fileprepper merge -i sales.csv products.csv -o step1.csv \
  -t Horizontal --join-type Inner --key-columns "ProductID"

fileprepper merge -i step1.csv regions.csv -o final.csv \
  -t Horizontal --join-type Left --key-columns "RegionID"
```

### Format Conversion

```bash
# CSV to JSON for API
fileprepper convert-format -i data.csv -o data.json -t JSON

# CSV to XML for legacy system
fileprepper convert-format -i data.csv -o data.xml -t XML

# TSV to CSV for Excel
fileprepper convert-format -i export.tsv -o import.csv -t CSV
```

## Time Series Processing

### Resample and Aggregate

```bash
# Create daily aggregates from hourly data
fileprepper aggregate -i hourly.csv -o daily.csv \
  --group-by "Date" \
  --aggregates "Value:Average:AvgValue,Value:Sum:SumValue,Value:Max:MaxValue,Value:Min:MinValue"
```

### Create Time Features

```bash
# Extract temporal features
fileprepper extract-date -i timeseries.csv -o features.csv \
  --extractions "Timestamp:Year,Month,DayOfWeek,Hour,Quarter"

# Create lag features
fileprepper create-lag-features -i timeseries.csv -o lagged.csv \
  --group-by "Series" --time-column "Timestamp" \
  --lag-columns "Value" --lag-periods "1,7,30,365"
```

### Handle Seasonality

```bash
# Normalize by month/season
fileprepper normalize -i seasonal.csv -o normalized.csv \
  -c "Sales" -m ZScore
```

## Data Quality

### Detect and Remove Outliers

```bash
# Filter extreme values
fileprepper filter-rows -i data.csv -o no_outliers.csv \
  --conditions "Age:LessThan:120"

fileprepper filter-rows -i data.csv -o filtered.csv \
  --conditions "Salary:GreaterThan:10000"
```

### Validate and Clean

```bash
# Remove invalid emails
fileprepper filter-rows -i contacts.csv -o valid.csv \
  --conditions "Email:Contains:@"

# Keep only active records
fileprepper filter-rows -i data.csv -o active.csv \
  --conditions "Status:Equals:Active"
```

### Standardize Values

```bash
# Replace inconsistent values
fileprepper replace -i data.csv -o clean.csv \
  --replacements "Status:N/A=Pending;Unknown=Pending;Missing=Pending"

# Standardize categories
fileprepper replace -i data.csv -o clean.csv \
  --replacements "Country:US=United States;USA=United States"
```

## ETL Pipelines

### Complete Data Pipeline

```bash
#!/bin/bash
# data_pipeline.sh - Complete ETL workflow

INPUT="raw_data.csv"
OUTPUT="final_output.json"

# 1. Data Cleaning
echo "Step 1: Cleaning data..."
fileprepper remove-columns -i $INPUT -o step1.csv \
  -c "Debug,TempCol" --ignore-errors

fileprepper drop-duplicates -i step1.csv -o step2.csv \
  -c "ID,Email" --subset-only --keep-first

# 2. Handle Missing Values
echo "Step 2: Filling missing values..."
fileprepper fill-missing -i step2.csv -o step3.csv \
  -m "Age:Mean,Salary:Mean" --ignore-errors

# 3. Data Transformation
echo "Step 3: Transforming data..."
fileprepper convert-type -i step3.csv -o step4.csv \
  -c "Date:DateTime:yyyy-MM-dd,Age:Integer,Active:Boolean"

fileprepper normalize -i step4.csv -o step5.csv \
  -c "Age,Salary,Score" -m MinMax

# 4. Feature Engineering
echo "Step 4: Creating features..."
fileprepper extract-date -i step5.csv -o step6.csv \
  --extractions "Date:Year,Month,DayOfWeek"

# 5. Format Conversion
echo "Step 5: Converting format..."
fileprepper convert-format -i step6.csv -o $OUTPUT -t JSON

# 6. Cleanup
rm step*.csv

echo "Pipeline complete! Output: $OUTPUT"
```

### Incremental Processing

```bash
# Process new data and append to existing (the newer file first, so its rows are the ones kept)
fileprepper merge -i new_data.csv existing.csv -o updated.csv -t Vertical

fileprepper drop-duplicates -i updated.csv -o final.csv \
  -c "ID" --subset-only --keep-first  # Keeps the first row per ID: the newest, as the newer file came first
```

## Tips

1. **Chain commands** - Pipe through multiple transformations
2. **Use error handling** - Add `--ignore-errors` for dirty data
3. **Test small first** - Verify on sample before full dataset
4. **Backup originals** - Keep copies before transforming
5. **Script pipelines** - Automate recurring workflows
6. **Monitor resources** - Large files need adequate memory
7. **Version outputs** - Include dates in output filenames

## Performance Tips

```bash
# For large files, process in stages
# Instead of processing 10M rows at once:

# 1. Sample for development
fileprepper data-sampling -i huge.csv -o sample.csv -m Random --size 10000

# 2. Test pipeline on sample
# ... test commands on sample.csv ...

# 3. Run on full dataset when validated
# ... run pipeline on huge.csv ...
```

## More Examples

See also:
- [CLI Reference](CLI-Guide.md) - Complete command documentation
- [API Reference](API-Reference.md) - Programmatic usage
- [Quick Start](Quick-Start.md) - Getting started guide
