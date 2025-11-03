# Installation Guide

## Quick Install (Recommended)

### Install as Global Tool

```bash
# Install from NuGet (once published)
dotnet tool install -g FilePrepper.CLI

# Verify installation
fileprepper --help
```

### Use Anywhere

After installation, `fileprepper` command is available system-wide:

```bash
# From any directory
fileprepper normalize -i data.csv -o output.csv -c "Age,Salary" -m MinMax
```

## Alternative Methods

### Install from Local Build

```bash
# Clone and build
git clone <repository-url>
cd FilePrepper

# Build package
cd src/FilePrepper.CLI
dotnet pack -c Release

# Install from local package
dotnet tool install -g FilePrepper.CLI --add-source ./nupkg

# Verify
fileprepper --help
```

### Run from Source (No Install)

```bash
# Clone repository
git clone <repository-url>
cd FilePrepper

# Build
dotnet build src/FilePrepper.CLI

# Run directly
cd src/FilePrepper.CLI
dotnet run -- --help
dotnet run -- normalize -i data.csv -o output.csv -c "Age" -m MinMax
```

## Update

```bash
# Update to latest version
dotnet tool update -g FilePrepper.CLI
```

## Uninstall

```bash
# Remove global tool
dotnet tool uninstall -g FilePrepper.CLI
```

## Verify Installation

```bash
# Check installed version
dotnet tool list -g | grep FilePrepper

# Test command
fileprepper --help
```

## Requirements

- .NET 9.0 SDK or later
- Supported platforms: Windows, Linux, macOS

### Install .NET SDK

If you don't have .NET installed:

**Windows:**
```powershell
winget install Microsoft.DotNet.SDK.9
```

**macOS:**
```bash
brew install dotnet@9
```

**Linux (Ubuntu/Debian):**
```bash
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 9.0
```

## Troubleshooting

### "Command not found" after install

Check that .NET tools directory is in your PATH:

**Windows:**
```powershell
# Tools installed to: %USERPROFILE%\.dotnet\tools
# Add to PATH if needed
$env:PATH += ";$env:USERPROFILE\.dotnet\tools"
```

**Linux/macOS:**
```bash
# Tools installed to: ~/.dotnet/tools
# Add to PATH if needed
export PATH="$PATH:$HOME/.dotnet/tools"

# Make permanent (add to ~/.bashrc or ~/.zshrc)
echo 'export PATH="$PATH:$HOME/.dotnet/tools"' >> ~/.bashrc
```

### "Tool already installed"

Update instead:
```bash
dotnet tool update -g FilePrepper.CLI
```

Or reinstall:
```bash
dotnet tool uninstall -g FilePrepper.CLI
dotnet tool install -g FilePrepper.CLI
```

### Package not found

Clear NuGet cache and retry:
```bash
dotnet nuget locals all --clear
dotnet tool install -g FilePrepper.CLI
```

### Permission denied

**Linux/macOS:**
```bash
# Don't use sudo with dotnet tool
# Tools install to user directory, not system-wide
```

**Windows:**
```powershell
# Run PowerShell as Administrator if needed
```

## Using as Library

To use FilePrepper in your .NET project:

```bash
# Add package reference
dotnet add package FilePrepper

# Restore packages
dotnet restore
```

Then use in code:
```csharp
using FilePrepper.Tasks.NormalizeData;

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

## Next Steps

- [Quick Start Guide](docs/Quick-Start.md) - Get running in 5 minutes
- [CLI Reference](docs/CLI-Guide.md) - Complete command reference
- [Common Scenarios](docs/Common-Scenarios.md) - Real-world examples

## Get Help

- Documentation: See `/docs` directory
- Issues: Report on GitHub
- Command help: `fileprepper <command> --help`
