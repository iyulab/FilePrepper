# FilePrepper v0.4.0 Release Notes

**Release Date:** January 4, 2025

We're excited to announce FilePrepper v0.4.0, a major update that brings a complete CLI architecture rewrite with Microsoft's System.CommandLine framework and beautiful Spectre.Console terminal UI!

---

## 🎉 Highlights

### System.CommandLine Integration
FilePrepper now uses Microsoft's official command-line framework for a robust, modern CLI experience:
- ✅ Comprehensive argument validation
- ✅ Detailed help system for all commands
- ✅ Consistent command structure
- ✅ Better error messages and user guidance

### Rich Terminal UI with Spectre.Console
Beautiful, colorful terminal output that makes data processing delightful:
- 🎨 Color-coded success/error/warning messages
- ⏳ Progress indicators with spinners
- 📊 Formatted validation tables
- 📋 Summary panels with operation details
- ✨ Professional-grade terminal experience

### Complete Command Set
All 20 data processing commands fully migrated and enhanced:
- Data manipulation (add/remove/rename/reorder columns)
- Data transformation (type conversion, normalization, encoding)
- Data analysis (statistics, aggregation, lag features)
- Data organization (filtering, merging, deduplication, sampling)
- Format conversion (CSV, TSV, JSON, XML, Excel)

---

## 🚀 What's New

### Enhanced Commands

#### filter-rows
Filter data with multiple conditions and operators:
```bash
fileprepper filter-rows -i data.csv -o filtered.csv \
  -c "Age:GreaterThan:30,Status:Equals:Active" --verbose
```

**New operators:** Equals, NotEquals, GreaterThan, GreaterOrEqual, LessThan, LessOrEqual, Contains, NotContains, StartsWith, EndsWith

#### merge
Combine files vertically (stack) or horizontally (join):
```bash
# Vertical merge
fileprepper merge -i file1.csv file2.csv file3.csv -o merged.csv --type Vertical

# Horizontal join
fileprepper merge -i users.csv salaries.csv -o combined.csv \
  --type Horizontal --join-type Inner --key-columns "ID"
```

**Join types:** Inner, Left, Right, Full

#### fill-missing
Fill missing values with intelligent strategies:
```bash
fileprepper fill-missing -i data.csv -o filled.csv \
  --methods "Age:Mean,City:Mode,Score:Median" --verbose
```

**Fill methods:** Mean, Median, Mode, ForwardFill, BackwardFill, FixedValue

#### create-lag-features
Create lag features for time series machine learning:
```bash
fileprepper create-lag-features -i timeseries.csv -o features.csv \
  --group-by "PartNumber" --time-column "Date" \
  --lag-columns "Value,Temp" --lag-periods "1,2,3" \
  --target "FailureStatus" --verbose
```

Perfect for predictive maintenance and time series forecasting!

#### stats
Comprehensive statistical analysis:
```bash
fileprepper stats -i data.csv -o stats.csv \
  -c "Age,Salary,Score" \
  --stats "Mean,Median,StandardDeviation,Min,Max,Q1,Q3" --verbose
```

**Available statistics:** Mean, StandardDeviation, Min, Max, Median, Q1, Q3, ZScore, RobustZScore, PercentRank, MAD

### Improved User Experience

**Before (v0.3.x):**
```
Processing...
Done.
```

**After (v0.4.0):**
```
┌───────────────────────────────────────┐
│ Filtering rows...                     │
└───────────────────────────────────────┘

╭─────────────┬──────────────────────────╮
│ Parameter   │ Status                   │
├─────────────┼──────────────────────────┤
│ Input file  │ ✓ Valid                  │
│ Output dir  │ ✓ Valid                  │
│ Conditions  │ ✓ 2 condition(s) parsed  │
╰─────────────┴──────────────────────────╯

✓ Rows filtered successfully: output.csv

╭──────────────────────────────────────╮
│    Filter Rows Complete              │
├──────────────────────────────────────┤
│ Summary:                             │
│ • Input: data.csv                    │
│ • Output: output.csv                 │
│ • Conditions: 2                      │
│ • Has header: true                   │
╰──────────────────────────────────────╯
```

---

## 📝 Documentation

### Complete CLI Guide Update
The CLI-Guide.md has been completely rewritten with:
- ✅ System.CommandLine syntax for all 20 commands
- ✅ Real-world examples and use cases
- ✅ Advanced usage patterns
- ✅ Pipeline workflows and batch processing
- ✅ Comprehensive troubleshooting section
- ✅ Tips and best practices

### New Test Suite
Comprehensive integration tests ensure quality:
- 69 integration tests across core commands
- CommandTestBase with test utilities
- FluentAssertions for readable tests
- Test coverage for all major features

---

## 🔄 Migration Guide

### Command Syntax Changes

All commands now require explicit input (`-i`) and output (`-o`) flags:

**v0.3.x (Old):**
```bash
fileprepper filter-rows data.csv -c "Age:>:30"
```

**v0.4.0 (New):**
```bash
fileprepper filter-rows -i data.csv -o filtered.csv -c "Age:GreaterThan:30"
```

### Operator Changes

Operators now use descriptive names instead of symbols:

| Old Syntax | New Syntax | Description |
|------------|------------|-------------|
| `Age:>:30` | `Age:GreaterThan:30` | Greater than |
| `Age:<:50` | `Age:LessThan:50` | Less than |
| `Age:>=:18` | `Age:GreaterOrEqual:18` | Greater or equal |
| `Age:<=:65` | `Age:LessOrEqual:65` | Less or equal |
| `Name:=:Alice` | `Name:Equals:Alice` | Equals |
| `Name:!=:Bob` | `Name:NotEquals:Bob` | Not equals |

### Breaking Changes

1. **Positional Arguments Removed**
   - Must use `-i` and `-o` flags explicitly
   - No more implicit argument parsing

2. **Operator Names Required**
   - Use full operator names (GreaterThan, LessThan, etc.)
   - Symbols (>, <, etc.) no longer supported

3. **Command Aliases**
   - Some command aliases changed for consistency
   - Use `--help` to see current command names

### Non-Breaking Changes

All functionality from v0.3.x is preserved:
- ✅ Same data processing capabilities
- ✅ Same file format support
- ✅ Same transformation operations
- ✅ Better syntax and error messages

---

## 💻 Installation & Upgrade

### New Installation

```bash
dotnet tool install -g fileprepper-cli
```

### Upgrade from v0.3.x

```bash
dotnet tool update -g fileprepper-cli
```

### Verify Installation

```bash
fileprepper --version
# Should show: 0.4.0

fileprepper --help
# Shows all available commands with descriptions
```

---

## 🛠️ Technical Details

### Dependencies

**Added:**
- System.CommandLine v2.0.0-beta4.22272.1
- Spectre.Console v0.49.2
- Spectre.Console.Cli v0.49.2

**Removed:**
- CommandLineParser (replaced by System.CommandLine)

### Architecture Changes

**New Structure:**
- `Commands/` directory with all command implementations
- `BaseCommand` class for shared functionality
- `CommonOptions` static class for consistent options
- `ExitCodes` constants for standardized error handling

**Test Infrastructure:**
- xUnit test framework with 69 integration tests
- FluentAssertions for readable assertions
- CommandTestBase for test utilities
- Real CSV file testing with temp directories

### Performance

- No performance regressions compared to v0.3.x
- Improved validation performance with System.CommandLine
- Better memory management in test suite

---

## 📊 Statistics

- **20 commands** fully migrated
- **69 integration tests** covering core functionality
- **784 lines** of comprehensive CLI documentation
- **Zero breaking changes** to data processing logic
- **100% test pass rate**

---

## 🎯 Use Cases

### Data Cleaning Pipeline
```bash
# Complete data preparation workflow
fileprepper fill-missing -i raw.csv -o step1.csv --methods "Age:Mean,City:Mode"
fileprepper filter-rows -i step1.csv -o step2.csv -c "Age:GreaterThan:0"
fileprepper normalize -i step2.csv -o step3.csv -c "Age,Salary" -m MinMax
fileprepper convert-format -i step3.csv -o final.json -f JSON --pretty
```

### Time Series Feature Engineering
```bash
# Create lag features for machine learning
fileprepper create-lag-features -i sensor_data.csv -o ml_features.csv \
  --group-by "SensorID" --time-column "Timestamp" \
  --lag-columns "Temperature,Pressure,Vibration" --lag-periods "1,2,3,6,12" \
  --target "FailureFlag" --verbose
```

### Data Integration
```bash
# Merge datasets from multiple sources
fileprepper merge -i sales_q1.csv sales_q2.csv sales_q3.csv -o annual.csv --type Vertical
fileprepper merge -i customers.csv orders.csv -o customer_orders.csv \
  --type Horizontal --join-type Left --key-columns "CustomerID"
```

### Statistical Analysis
```bash
# Generate comprehensive statistics report
fileprepper stats -i survey_data.csv -o statistics.csv \
  -c "Age,Income,Satisfaction" \
  --stats "Mean,Median,StandardDeviation,Q1,Q3,Min,Max" --verbose
```

---

## 🐛 Known Issues

None at this time. Report issues at: https://github.com/iyulab-rnd/FilePrepper/issues

---

## 🙏 Acknowledgments

Special thanks to:
- Microsoft for the excellent System.CommandLine framework
- Spectre.Console team for the beautiful terminal UI library
- All contributors and users who provided feedback

---

## 📚 Resources

- **Documentation:** [GitHub Wiki](https://github.com/iyulab-rnd/FilePrepper/tree/main/docs)
- **CLI Reference:** [CLI-Guide.md](https://github.com/iyulab-rnd/FilePrepper/blob/main/docs/CLI-Guide.md)
- **Examples:** [Common-Scenarios.md](https://github.com/iyulab-rnd/FilePrepper/blob/main/docs/Common-Scenarios.md)
- **NuGet Package:** [fileprepper-cli](https://www.nuget.org/packages/fileprepper-cli/)
- **Issue Tracker:** [GitHub Issues](https://github.com/iyulab-rnd/FilePrepper/issues)

---

## 🔮 What's Next?

### Planned for v0.5.0
- Interactive mode for step-by-step operations
- Configuration file support (.fileprep.json)
- Batch processing with job definitions
- Plugin system for custom transformations

### Future Roadmap
- Performance optimizations for very large files (>10GB)
- Cloud storage integration (S3, Azure Blob)
- Real-time data streaming support
- GUI application for visual data transformation

---

## 📞 Get Help

- **Questions:** Open a [GitHub Discussion](https://github.com/iyulab-rnd/FilePrepper/discussions)
- **Bug Reports:** [GitHub Issues](https://github.com/iyulab-rnd/FilePrepper/issues)
- **Feature Requests:** [GitHub Issues](https://github.com/iyulab-rnd/FilePrepper/issues) with `enhancement` label

---

**Happy Data Processing! 🎉**

*The FilePrepper Team*
