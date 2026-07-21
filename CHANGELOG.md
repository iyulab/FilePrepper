# Changelog

All notable changes to FilePrepper will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.7.4]

### Security

- **Lifted the transitive `System.Security.Cryptography.Xml` floor to 10.0.10.** EPPlus 8.6.1 (the latest stable) resolves it at 10.0.7, against which four High-severity advisories were published (GHSA-cvvh-rhrc-wg4q, GHSA-g8r8-53c2-pm3f, GHSA-23rf-6693-g89p, GHSA-8q5v-6pqq-x66h) — a new batch, distinct from the pair fixed by the 0.7.2 EPPlus upgrade. The package now carries a direct reference so the fixed floor ships in FilePrepper's own dependency graph and every consumer inherits it, instead of each consumer re-pinning downstream. To be dropped when EPPlus raises its own floor.

## [0.7.3]

### Fixed

- **`EncodingDetector` no longer misdetects large UTF-8 files as CP949** when a multibyte UTF-8 sequence straddles the 64KB detection-buffer boundary. `IsValidUtf8` previously treated a sequence truncated at the sample boundary as *invalid* (returning `false`), so the file fell through to the Korean-pattern check and could be classified as CP949 — corrupting Korean column names/labels on read. Truncation at the buffer boundary is now treated as *incomplete-but-valid* (the detector stops scanning), matching a correct UTF-8 validator. Detection of genuine CP949/EUC-KR and small UTF-8 files is unchanged. This makes `EncodingDetector` a reliable single authority for consumers converging their own CP949→UTF-8 duplication onto it.

## [0.7.2]

### Changed

- **Dependency modernization** — EPPlus 8.5.4 → 8.6.1, ExcelDataReader / ExcelDataReader.DataSet 3.8.0 → 3.9.0, `Microsoft.Extensions.*` 10.0.8 → 10.0.10, System.CommandLine 2.0.8 → 2.0.10. Verified `dotnet list package --vulnerable --include-transitive` is clean (EPPlus 8.6.1 carries no known advisories) and all 370 tests pass. `Spectre.Console` deliberately held at 0.55.2 to stay paired with `Spectre.Console.Cli` 0.55.0 (latest stable `.Cli`); bumping the core ahead of `.Cli` risks pre-1.0 API skew.

## [0.7.1]

### Security

- **Transitive vulnerability remediated (EPPlus 8.5.3 → 8.5.4)** — EPPlus 8.5.3 pulled `System.Security.Cryptography.Xml` 10.0.0, which carries known high-severity advisories (GHSA-37gx-xxp4-5rgx, GHSA-w3x6-4m5h-cxqf). EPPlus 8.5.4 raises its declared floor to the patched 10.0.7. This release publishes that already-in-main dependency bump so consumers resolve a non-vulnerable graph without needing a consumer-side pin. Verified via `dotnet list package --vulnerable --include-transitive` (no vulnerable packages) and by a downstream consumer (MLoop) building clean after removing its temporary pin.

### Changed

- Accumulated dependency bumps since 0.7.0 (all Dependabot, no public API change): System.CommandLine 2.0.7 → 2.0.8, Microsoft.Extensions group (5 packages), Microsoft.NET.Test.Sdk 18.4.0 → 18.5.1.

> **Note (changelog drift):** the `[Unreleased]` entries below predate 0.7.0 and were already shipped in 0.5.x–0.7.0 releases (no corresponding feature commits exist after the 0.7.0 version bump); they remain here pending a separate history-reconciliation pass and are **not** newly introduced by 0.7.1.

## [Unreleased]

### Added

- **Auto Encoding Detection** - Automatic file encoding detection for CP949/EUC-KR Korean files
  - `--encoding` option for all commands (auto, utf-8, cp949, euc-kr)
  - BOM detection, UTF-8 validation, and CP949 Korean pattern detection
  - Default `auto` mode detects encoding automatically
  - New `EncodingDetector` utility in FilePrepper.Utils

- **Skip Rows (Multi-Header CSV)** - Skip metadata rows before the actual CSV header
  - `--skip-rows` option for all commands (default: 0)
  - Handles files with title rows, notes, or multi-line headers above actual data

- **Remove Constants** - Auto-detect and remove constant/near-constant columns
  - `remove-constants` command with `--threshold` and `--report-only` options
  - `UniqueRatioThreshold` for fine-grained control (0.0 = exact constants only)
  - Report-only mode for analysis without data modification

- **Merge Glob Pattern** - Merge multiple files using glob patterns
  - `--input-pattern` / `-p` option for merge command (e.g., `data/*.csv`)
  - Alternative to listing files individually with `--input`

### Changed

- Renamed `FileFormatConvertOption.Encoding` to `OutputEncoding` to avoid conflict with base encoding option
- `--encoding` on `convert-format` command renamed to `--output-encoding`

## [0.4.9] - 2026-01-11

### Added

- 🔄 **Unpivot (Wide-to-Long Transformation)** - Transform columnar data into row format
  - `Unpivot()` method for complex multi-column group transformations
  - `UnpivotSimple()` helper for simple single-value column unpivoting
  - `UnpivotColumnGroup` class for defining column groupings with index values
  - Support for multiple value columns per group (e.g., Date + Quantity pairs)
  - `skipEmptyRows` option to exclude rows with all empty values
  - Validation for column group consistency and value column matching
  - ✅ 6 comprehensive tests (100% passing)

- 📁 **Filename Metadata Extraction** - Extract metadata from filenames during CSV concatenation
  - New `ConcatCsvAsync()` overload with `FilenameMetadataOptions` parameter
  - `FilenameMetadataOptions` class for configuring extraction behavior
  - `FilenameMetadataPreset` enum with 4 built-in patterns:
    - `DateOnly`: Extract dates from `data_2024-01-15.csv` format
    - `SensorDate`: Extract dates from `sensor-2021.09.06.csv` format
    - `Manufacturing`: Extract BatchId and Category from `batch_001_normal.csv` format
    - `Category`: Extract category labels (normal, outlier, train, test, valid)
  - Custom regex patterns via `CustomPatterns` dictionary
  - Configurable source column and date column names
  - ✅ 5 comprehensive tests (100% passing)

### Usage Examples

**Unpivot - Transform Shipment Data:**
```csharp
// Wide format: Order, 1차_Date, 1차_Qty, 2차_Date, 2차_Qty
var result = pipeline.Unpivot(
    baseColumns: new[] { "Order", "Product" },
    columnGroups: new[]
    {
        new UnpivotColumnGroup { Columns = new[] { "1차_Date", "1차_Qty" }, IndexValue = "1" },
        new UnpivotColumnGroup { Columns = new[] { "2차_Date", "2차_Qty" }, IndexValue = "2" }
    },
    indexColumn: "Shipment",
    valueColumns: new[] { "Date", "Qty" });
// Result: Order, Product, Shipment, Date, Qty (long format)
```

**Simple Unpivot - Quarterly Data:**
```csharp
var result = pipeline.UnpivotSimple(
    baseColumns: new[] { "Region" },
    unpivotColumns: new[] { "Q1", "Q2", "Q3", "Q4" },
    indexColumn: "Quarter",
    valueColumn: "Sales");
// Result: Region, Quarter, Sales
```

**Filename Metadata - Sensor Data with Dates:**
```csharp
var data = await DataPipeline.ConcatCsvAsync(
    "sensor-*.csv",
    "dataset/",
    hasHeader: true,
    new FilenameMetadataOptions
    {
        Preset = FilenameMetadataPreset.SensorDate
    });
// Result: Original columns + SourceFile + FileDate
```

**Custom Metadata Extraction:**
```csharp
var data = await DataPipeline.ConcatCsvAsync(
    "region-*.csv",
    directory,
    hasHeader: true,
    new FilenameMetadataOptions
    {
        CustomPatterns = new Dictionary<string, string>
        {
            ["Region"] = @"region-(\w+)_",
            ["Period"] = @"_(\d{4}-\d{2})"
        }
    });
// Result: Original columns + SourceFile + Region + Period
```

## [0.4.8] - 2026-01-10

### Changed

- 🔧 **System.CommandLine Migration** - Upgraded from 2.0.0-beta4.22272.1 to 2.0.0 stable
  - **Breaking API Changes Implemented:**
    - `SetHandler` → `SetAction` with `ParseResult` parameter
    - Handler signature: `context.ExitCode = await ...` → `return await ...`
    - `GetValueForOption()` → `GetValue()`
    - `AddCommand()` → `Add()` for subcommands
    - `AddGlobalOption()` → `Add()` + `Recursive = true`
    - Option constructor: old 2-param format → object initializer syntax
    - Option properties: `getDefaultValue` → `DefaultValueFactory`, `parseArgument` → `CustomParser`
    - `Symbol.Name` is now read-only (removed from object initializer)
    - Test methods: `command.InvokeAsync(args)` → `command.Parse(args).InvokeAsync()`
  - **Migration Results:**
    - All 28 command files successfully migrated
    - All 32 source files updated
    - Build: ✅ 0 errors
    - Tests: ✅ 344/344 passed (281 core + 63 CLI tests)
  - **Impact:** Internal CLI implementation only - no changes to user-facing commands or options

## [0.4.4] - 2025-11-11

### Added

- 📊 **GroupBy/Aggregate Operations (P0 - Critical)** - Time-series batch aggregation
  - `GroupBy(string keyColumn)` returns `GroupedDataPipeline` for aggregation
  - `Aggregate()` with 10 aggregation methods: Mean, Sum, Min, Max, Count, Std, Var, Median, First, Last
  - Hash-based grouping with Dictionary for O(1) lookup performance
  - Sample standard deviation (n-1 denominator) for statistical accuracy
  - Multiple aggregations per column with automatic suffix naming
  - Custom suffix format support for output column names
  - Handles edge cases: empty groups, null keys, non-numeric values
  - ✅ 19 comprehensive tests (100% passing)

- 🔗 **Join Operations (P1 - High)** - Combine multiple data sources
  - 4 join types: `Inner`, `Left`, `Right`, `Outer` (JoinType enum)
  - `Join()` method with hash join algorithm (O(1) lookup using Dictionary)
  - Duplicate key handling (creates Cartesian product for 1:N joins)
  - Column selection with `selectColumns` parameter
  - Column collision resolution with automatic `_right` suffix
  - Prefix support (`leftPrefix`, `rightPrefix`) for namespace control
  - Smart key preservation: Right/Outer joins preserve right key value when left row is null
  - ✅ 18 comprehensive tests (100% passing)

- 📈 **Statistical Functions (P2 - Enhancement)** - Data exploration and analysis
  - `GetStatistics(string column)` returns comprehensive `ColumnStatistics` record
    - Mean, Std (sample standard deviation), Min, Max
    - Median, Q1, Q3 (quartiles with linear interpolation)
    - IQR (Interquartile Range), Variance (sample variance)
    - Count (valid numeric values), NullCount (null/non-numeric)
  - `Normalize(string column, NormalizationMethod method, string? outputColumn)` with 3 methods
    - **ZScore**: (x - mean) / std → Mean=0, Std=1
    - **MinMax**: (x - min) / (max - min) → [0, 1] range
    - **Robust**: (x - median) / IQR → Robust to outliers
  - Extended `NormalizationMethod` enum with Robust method
  - Validation for edge cases (constant values, zero IQR)
  - ✅ 22 comprehensive tests (100% passing)

### Usage Examples

**GroupBy/Aggregate - Batch Sensor Data:**
```csharp
var aggregated = await DataPipeline
    .FromCsvAsync("sensor_data.csv")
    .GroupBy("batch_id")
    .Aggregate(new[]
    {
        ("temperature", AggregationMethod.Mean),
        ("temperature", AggregationMethod.Std),
        ("pressure", AggregationMethod.Min),
        ("pressure", AggregationMethod.Max)
    });
// Output: batch_id, temperature_mean, temperature_std, pressure_min, pressure_max
```

**Join Operations - Sensor + Quality Labels:**
```csharp
var joined = aggregatedSensorData.Join(
    qualityLabels,
    leftKey: "batch_id",
    rightKey: "batch_id",
    joinType: JoinType.Inner,
    selectColumns: new[] { "defect_rate", "quality_score" }
);
```

**Statistical Analysis:**
```csharp
var stats = data.GetStatistics("temperature");
Console.WriteLine($"Mean: {stats.Mean}, Std: {stats.Std}, IQR: {stats.IQR}");

var normalized = data
    .Normalize("temperature", NormalizationMethod.ZScore)
    .Normalize("pressure", NormalizationMethod.MinMax);
```

**Complete ML Pipeline:**
```csharp
var result = await DataPipeline
    .FromCsvAsync("raw_sensor_data.csv")
    .GroupBy("batch_id")
    .Aggregate(new[] {
        ("temp_zone1", AggregationMethod.Mean),
        ("temp_zone1", AggregationMethod.Std)
    })
    .Join(await DataPipeline.FromCsvAsync("quality.csv"),
          "batch_id", "batch_id", JoinType.Inner)
    .Normalize("temp_zone1_mean", NormalizationMethod.ZScore)
    .ToCsvAsync("ml_ready.csv");
```

### Technical Details

**GroupBy/Aggregate Architecture:**
- New `GroupedDataPipeline` class for fluent aggregation API
- Single-pass grouping algorithm with Dictionary<string, List<row>>
- Extended `AggregationMethod` enum (Var, Median, First, Last added)
- Comprehensive error messages with available column suggestions

**Join Operations Architecture:**
- New `JoinType` enum (Inner, Left, Right, Outer)
- Hash join implementation using Dictionary<key, List<rows>>
- Duplicate key handling with Cartesian product generation
- Column collision detection and automatic resolution
- Optimized CreateJoinedRow() helper for row construction

**Statistical Functions Architecture:**
- New `ColumnStatistics` record with comprehensive metrics
- Percentile calculation using linear interpolation
- Robust error handling for constant values and edge cases
- Extended `NormalizationMethod` enum (ZScore, MinMax, Robust)

### Changed

- 📚 **API Documentation Updated** - docs/API-Reference.md
  - Version updated to v0.4.4
  - Added GroupedDataPipeline class reference
  - Added ColumnStatistics record reference
  - Added comprehensive usage examples for new features
  - Updated enum documentation (JoinType, AggregationMethod)

### Test Coverage

- ✅ **59 New Tests Added** (All Passing)
  - GroupByAggregateTests: 19 tests
  - JoinOperationsTests: 18 tests
  - StatisticalFunctionsTests: 22 tests
- ✅ **Total: 276 tests** (100% passing)
- ✅ **Performance validated** with 10K row datasets

### Impact

- **Unblocks**: Dataset 012 preprocessing (sensor aggregation + quality label join)
- **Reduces Code**: 80+ lines → 10 lines (87% reduction for Dataset 012 scenario)
- **Enables**: Advanced analytics workflows with fluent API pattern
- **Performance**: Hash-based algorithms ensure O(1) or O(n) efficiency

### Bug Fixes

- 🐛 **Join Key Preservation** - Fixed Right/Outer join key value handling
  - Issue: Right-only rows had empty key column instead of right key value
  - Fix: Smart key preservation logic in CreateJoinedRow() when leftRow is null
  - Impact: Right and Outer joins now correctly preserve join key values

## [0.4.3] - 2025-11-10

### Added
- 🚀 **Multi-File CSV Concatenation** - `ConcatCsvAsync()` for Dataset Support
  - Concatenate multiple CSV files matching a pattern (e.g., `kemp-*.csv`)
  - Automatic header validation with clear error messages
  - Alphabetical file ordering for predictable results
  - Optional source tracking column to identify file origin
  - Memory-efficient streaming processing for 100+ files
  - Enables processing of split datasets (e.g., Dataset 010 with 33 files)

- 🌏 **Korean Time Format Parsing** - `ParseKoreanTime()` for Localization
  - Parse Korean AM/PM time format ("오전 9:01:18", "오후 2:15:30")
  - Automatic 12/24-hour conversion with edge case handling
  - Configurable base date for time-only data
  - Seamless integration with `ExtractDateFeatures()`
  - Supports Korean manufacturing dataset preprocessing

### Usage Examples

**Multi-File Concatenation:**
```csharp
// Concatenate 33 CSV files into single pipeline
var data = await DataPipeline.ConcatCsvAsync(
    pattern: "kemp-*.csv",
    directory: "dataset/",
    hasHeader: true,
    addSourceColumn: true  // Track source file
);

Console.WriteLine($"Loaded {data.RowCount} rows from multiple files");
```

**Korean Time Parsing:**
```csharp
var pipeline = await DataPipeline.FromCsvAsync("data.csv")
    .ParseKoreanTime("Time", "ParsedTime")
    .ExtractDateFeatures("ParsedTime", DateFeatures.Hour | DateFeatures.Minute)
    .ToDataFrame();

// "오전 9:01:18" → Hour: 9, Minute: 1
// "오후 2:15:30" → Hour: 14, Minute: 15
```

**Combined Workflow (Dataset 010 Scenario):**
```csharp
var result = await DataPipeline.ConcatCsvAsync("kemp-*.csv", datasetDir)
    .ParseKoreanTime("Time", "ParsedTime")
    .ExtractDateFeatures("ParsedTime", DateFeatures.Hour | DateFeatures.Minute)
    .Select(new[] { "ParsedTime_Hour", "Temp", "Press", "Vib" })
    .ToCsvAsync("processed_data.csv");
```

### Technical Details

**ConcatCsvAsync Features:**
- Streaming file processing (no full dataset in memory)
- Header schema validation across all files
- Graceful handling of empty file matches
- Informative exceptions with filename context
- Compatible with all Pipeline transformations

**ParseKoreanTime Features:**
- Edge case handling: 오전 12:00:00 (midnight), 오후 12:00:00 (noon)
- Graceful error handling for invalid formats
- Configurable base date for time-only columns
- Returns ISO 8601 format ("yyyy-MM-dd HH:mm:ss")

### Test Coverage
- ✅ 14 new comprehensive tests (100% passing)
  - 5 ConcatCsvAsync tests (basic, source tracking, validation, ordering, empty)
  - 6 ParseKoreanTime tests (AM, PM, edge cases, invalid, integration)
  - 1 Dataset010 end-to-end scenario test
  - Total test count: 212 tests

### Impact
- **Unblocks**: Dataset 010 (33 files), Dataset 012 (6 files), Dataset 013 (5 files)
- **Enables**: Korean manufacturing dataset support
- **Use Cases**: Multi-file ML datasets, localized time data, split CSV processing

## [0.4.1] - 2025-01-09

### Added
- ✨ **Multi-Format Support for Pipeline API** - Excel, JSON, and XML
  - `FromExcelAsync()` - Read Excel files (.xls, .xlsx) with sheet selection
  - `FromJsonAsync()` - Read JSON array of objects
  - `FromXmlAsync()` - Read XML with customizable row element
  - `ToExcelAsync()` - Write to Excel with custom sheet names
  - `ToJsonAsync()` - Write to JSON with indentation control
  - `ToXmlAsync()` - Write to XML with customizable root and row elements
  - Seamless format conversion (e.g., Excel → JSON, CSV → XML)
  - All Pipeline API transformations work across all formats

- 📊 **Enhanced Documentation**
  - Multi-format Pipeline API examples in README.md
  - Complete API reference with format methods
  - Cross-format transformation examples
  - Format conversion best practices

- ✅ **Comprehensive Test Coverage** - 198 total tests (100% passing)
  - 9 new multi-format integration tests (MultiFormatPipelineTests)
  - Excel read/write with EPPlus 8.2.1
  - JSON serialization/deserialization
  - XML parsing and generation
  - Cross-format transformation validation
  - Custom sheet names and XML element names

### Changed
- 🔧 **EPPlus License Configuration** - Updated for version 8+
  - NonCommercial license setup using `SetNonCommercialPersonal()`
  - Proper EPPlus 8.2.1 API usage
  - No compilation warnings for license configuration

### Technical Details

**New Dependencies:**
- EPPlus 8.2.1 (already present, now fully utilized)
- System.Text.Json (built-in .NET)
- System.Xml.Linq (built-in .NET)

**Pipeline API Enhancements:**
- 6 new factory methods (FromExcelAsync, FromJsonAsync, FromXmlAsync)
- 3 new output methods (ToExcelAsync, ToJsonAsync, ToXmlAsync)
- ExcelUtils.WriteExcelFileAsync() for Excel file creation
- Full bidirectional format support (read any format, write to any format)

**Performance:**
- Maintains 67-90% file I/O reduction efficiency
- In-memory transformations across all formats
- Minimal overhead for format conversion

### Known Issues

None at this time.

---

## [0.4.0] - 2025-01-04

### Added
- ✨ **System.CommandLine Integration** - Microsoft's official command-line framework
  - Robust argument parsing and validation
  - Comprehensive help system with detailed command descriptions
  - Consistent command structure across all 20 commands

- 🎨 **Spectre.Console Rich UI** - Beautiful terminal experience
  - Color-coded output for success, errors, and warnings
  - Progress indicators with spinners for long-running operations
  - Formatted validation tables showing parameter status
  - Summary panels with operation details

- 📝 **Enhanced CLI Commands** - All 20 commands with improved syntax
  - `filter-rows` - Filter with multiple conditions and operators
  - `merge` - Vertical and horizontal merge with join types
  - `fill-missing` - Multiple fill strategies (Mean, Median, Mode, etc.)
  - `create-lag-features` - Time series lag feature engineering
  - `stats` - Comprehensive statistics (Mean, Median, StdDev, Quartiles, etc.)
  - `aggregate` - Group by and aggregate operations
  - `normalize` / `scale` - Data normalization and scaling
  - `one-hot-encoding` - Categorical variable encoding
  - `convert-type` - Type conversion with format support
  - `extract-date` - Date component extraction
  - `drop-duplicates` - Duplicate row removal
  - `data-sampling` - Random, systematic, and stratified sampling
  - `convert-format` - Format conversion (CSV, JSON, XML, Excel)
  - `add-columns` / `remove-columns` / `rename-columns` / `reorder-columns` - Column operations
  - `column-interaction` - Mathematical operations between columns
  - `replace` - Value replacement in columns

- ✅ **Integration Test Suite** - Comprehensive test coverage
  - CommandTestBase with test helpers and utilities
  - FilterRowsCommandTests (13 tests)
  - MergeCommandTests (14 tests)
  - FillMissingValuesCommandTests (14 tests)
  - CreateLagFeaturesCommandTests (13 tests)
  - BasicStatisticsCommandTests (15 tests)
  - Total: 69 integration tests

- 📚 **Complete Documentation Update** - Updated CLI-Guide.md
  - System.CommandLine syntax for all commands
  - Detailed examples with real-world use cases
  - Advanced usage patterns and pipeline workflows
  - Comprehensive troubleshooting section
  - Tips and best practices

### Changed
- 🔄 **Complete CLI Architecture Rewrite**
  - Migrated from CommandLineParser to System.CommandLine
  - BaseCommand class with shared functionality
  - Improved error handling and user feedback
  - Better validation with detailed error messages

- 📊 **Enhanced User Experience**
  - Rich terminal output with colors and formatting
  - Progress indicators for all operations
  - Validation tables showing parameter status
  - Success/error messages with icons
  - Verbose mode for detailed operation logging

- 🛠️ **Improved Command Options**
  - Consistent flag naming across all commands
  - Short aliases for common options (-i, -o, -c, -v)
  - Better default values and optional parameters
  - Clear required vs optional distinction

### Deprecated
- ⚠️ **CommandLineParser-based CLI** - Legacy Tools/ directory
  - Old command implementations excluded from build
  - CommandLineParser package dependency removed
  - Legacy ICommandHandler and ICommandParameters interfaces removed
  - Users should migrate to new System.CommandLine syntax (see Migration Guide below)

### Migration Guide

#### Command Syntax Changes

**Old (CommandLineParser):**
```bash
fileprepper filter-rows input.csv -c "Age:>:30"
```

**New (System.CommandLine):**
```bash
fileprepper filter-rows -i input.csv -o output.csv -c "Age:GreaterThan:30" --verbose
```

#### Key Differences

1. **Required Flags**: Input (`-i`) and output (`-o`) now use explicit flags
2. **Operator Names**: Use full names (GreaterThan, LessThan) instead of symbols (>, <)
3. **Verbose Mode**: New `--verbose` or `-v` flag for detailed output
4. **Better Help**: All commands have comprehensive `--help` documentation

#### Breaking Changes

- ❌ Positional arguments no longer supported (must use `-i` and `-o` flags)
- ❌ Operator symbols replaced with named operators (Equals, GreaterThan, Contains, etc.)
- ❌ Some command aliases changed for consistency
- ✅ All functionality preserved with improved syntax

#### Migration Examples

```bash
# OLD: fileprepper merge file1.csv file2.csv -o merged.csv
# NEW:
fileprepper merge -i file1.csv file2.csv -o merged.csv --type Vertical

# OLD: fileprepper fill-missing input.csv -c Age -m mean
# NEW:
fileprepper fill-missing -i input.csv -o output.csv --methods "Age:Mean"

# OLD: fileprepper stats input.csv -c Score
# NEW:
fileprepper stats -i input.csv -o output.csv -c "Score" --stats "Mean,Median,StdDev"
```

### Technical Details

**Dependencies Updated:**
- Added: `System.CommandLine` v2.0.0-beta4.22272.1
- Added: `Spectre.Console` v0.49.2
- Added: `Spectre.Console.Cli` v0.49.2
- Removed: `CommandLineParser`

**Project Structure:**
- New `Commands/` directory with all command implementations
- BaseCommand class for shared functionality
- CommonOptions static class for consistent options
- ExitCodes constants for standardized error handling

**Testing Infrastructure:**
- xUnit test framework
- FluentAssertions for readable test assertions
- CommandTestBase for test utilities
- Integration tests for core commands

### Known Issues

None at this time.

### Security

- No security vulnerabilities identified
- All dependencies up to date with latest stable versions
- Input validation improved with System.CommandLine

---

## [0.3.1] - 2024-12-XX

### Added
- Excel file format support (.xlsx, .xls)
- create-lag-features command for time series analysis
- Enhanced error handling and logging

### Changed
- Improved performance for large file processing
- Better memory management for Excel operations

### Fixed
- CSV parsing issues with special characters
- Memory leaks in large file operations

---

## [0.3.0] - 2024-11-XX

### Added
- Initial CLI implementation
- Core data processing commands (15 commands)
- CSV, TSV, JSON, XML format support
- Basic statistics and data transformation operations

### Changed
- Refactored core library for better modularity
- Improved logging throughout application

---

## [0.2.x] - 2024-10-XX

### Added
- Core library functionality
- Task-based processing architecture
- Dependency injection support
- Logging infrastructure

---

## [Unreleased]

### Planned Features
- Interactive mode for step-by-step operations
- Configuration file support (.fileprep.json)
- Batch processing with job definitions
- Plugin system for custom transformations
- Performance optimizations for very large files (>10GB)
- Cloud storage integration (S3, Azure Blob)

---

## Release Notes

For detailed release notes and upgrade guides, see:
- [v0.4.0 Release Notes](docs/RELEASE_NOTES_v0.4.0.md)

## Links

- [GitHub Repository](https://github.com/iyulab/FilePrepper)
- [NuGet Package](https://www.nuget.org/packages/fileprepper-cli/)
- [Documentation](https://github.com/iyulab/FilePrepper/tree/main/docs)
- [Issue Tracker](https://github.com/iyulab/FilePrepper/issues)
