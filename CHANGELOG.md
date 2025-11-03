# Changelog

All notable changes to FilePrepper will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

- [GitHub Repository](https://github.com/iyulab-rnd/FilePrepper)
- [NuGet Package](https://www.nuget.org/packages/fileprepper-cli/)
- [Documentation](https://github.com/iyulab-rnd/FilePrepper/tree/main/docs)
- [Issue Tracker](https://github.com/iyulab-rnd/FilePrepper/issues)
