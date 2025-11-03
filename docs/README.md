# FilePrepper Documentation

Complete documentation for FilePrepper - ML Data Preprocessing Tool.

## 📚 Core Documentation

### Getting Started
- **[Quick Start Guide](Quick-Start.md)** - Get up and running in 5 minutes
- **[CLI Guide](CLI-Guide.md)** - Complete command reference with examples
- **[Common Scenarios](Common-Scenarios.md)** - Real-world use cases and workflows

### Advanced Usage
- **[API Reference](API-Reference.md)** - Programmatic usage for .NET developers
- **[Publishing Guide](Publishing.md)** - NuGet publishing instructions

### Project Management
- **[TASKS](TASKS.md)** - Project roadmap and development tasks

## 📦 Installation

```bash
# Install as global .NET tool
dotnet tool install -g fileprepper-cli

# Verify installation
fileprepper --version
```

See [Installation Guide](../INSTALL.md) for detailed instructions.

## 🚀 Quick Reference

### Basic Commands
```bash
# Normalize data
fileprepper normalize-data --input data.csv --output normalized.csv \
  --columns "Age,Salary" --method MinMax

# Fill missing values
fileprepper fill-missing-values --input data.csv --output filled.csv \
  --columns "Age,Salary" --method Mean

# Filter rows
fileprepper filter-rows --input sales.csv --output filtered.csv \
  --conditions "Revenue:GreaterThan:1000"

# Convert formats
fileprepper file-format-convert --input data.csv --output data.json \
  --format JSON
```

### Get Help
```bash
fileprepper --help              # List all commands
fileprepper <command> --help    # Command-specific help
fileprepper -v                  # Detailed version info
```

## 📋 Archive

Historical and release-specific documentation is available in the [archive/](archive/) directory:

- **Release Notes** - Version-specific release information
- **Test Reports** - Historical test status reports
- **Migration Guides** - Migration and refactoring documentation
- **Project Summaries** - Detailed project status snapshots

## 🔗 External Links

- **Main README**: [../README.md](../README.md)
- **Changelog**: [../CHANGELOG.md](../CHANGELOG.md)
- **Installation**: [../INSTALL.md](../INSTALL.md)
- **NuGet Package**: https://www.nuget.org/packages/fileprepper-cli
- **GitHub Repository**: https://github.com/iyulab/FilePrepper
- **Issues/Support**: https://github.com/iyulab/FilePrepper/issues

## 📂 Documentation Structure

```
docs/
├── README.md                    # This file - Documentation index
├── Quick-Start.md              # 5-minute getting started
├── CLI-Guide.md                # Complete command reference
├── Common-Scenarios.md         # Real-world examples
├── API-Reference.md            # Programmatic usage
├── Publishing.md               # Publishing guide
├── TASKS.md                    # Project roadmap
└── archive/                    # Historical documentation
    ├── RELEASE_NOTES_v0.4.0.md
    ├── RELEASE_CHECKLIST_v0.4.0.md
    ├── TEST_STATUS.md
    ├── MIGRATION_COMPLETE_2025-01-04.md
    ├── PROJECT_SUMMARY.md
    └── CLEANUP_SUMMARY.md
```

## 🤝 Contributing to Documentation

Documentation improvements are welcome! Please:
1. Keep examples practical and tested
2. Use consistent formatting
3. Update this index when adding new docs
4. Follow markdown best practices

---

**Version**: 0.4.0 | **Last Updated**: 2025-01-04
