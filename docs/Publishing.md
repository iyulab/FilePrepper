# Publishing Guide

Guide for publishing FilePrepper as a .NET Global Tool.

## Prerequisites

- .NET 9.0 SDK
- NuGet account (for publishing to NuGet.org)
- API key from NuGet.org

## Build Global Tool Package

### 1. Update Version

Edit `src/FilePrepper.CLI/FilePrepper.CLI.csproj`:

```xml
<Version>0.3.0</Version>
```

### 2. Build Package

```bash
cd src/FilePrepper.CLI
dotnet pack -c Release
```

**Output:** `./nupkg/FilePrepper.CLI.0.3.0.nupkg`

### 3. Test Locally

```bash
# Install from local package
dotnet tool install -g FilePrepper.CLI --add-source ./nupkg

# Test
fileprepper --help

# Uninstall
dotnet tool uninstall -g FilePrepper.CLI
```

## Publish to NuGet

### 1. Get API Key

1. Login to [nuget.org](https://www.nuget.org)
2. Go to Account Settings → API Keys
3. Create new API key with push permissions

### 2. Push Package

```bash
cd src/FilePrepper.CLI

# Push to NuGet
dotnet nuget push ./nupkg/FilePrepper.CLI.0.3.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

### 3. Verify

After a few minutes:
```bash
# Search for package
dotnet tool search FilePrepper

# Install from NuGet
dotnet tool install -g FilePrepper.CLI
```

## Version Management

### Semantic Versioning

- **Major (X.0.0):** Breaking changes
- **Minor (0.X.0):** New features, backward compatible
- **Patch (0.0.X):** Bug fixes

### Update Checklist

Before releasing new version:

1. ✅ Update version in `.csproj`
2. ✅ Update CHANGELOG.md
3. ✅ Run all tests: `dotnet test`
4. ✅ Build release: `dotnet pack -c Release`
5. ✅ Test locally
6. ✅ Push to NuGet
7. ✅ Create GitHub release
8. ✅ Update documentation

## Distribution Channels

### 1. NuGet.org (Primary)
```bash
dotnet tool install -g FilePrepper.CLI
```

### 2. GitHub Releases
- Attach `.nupkg` file
- Include release notes
- Tag version (e.g., v0.3.0)

### 3. Build from Source
```bash
git clone <repo-url>
cd FilePrepper
dotnet pack src/FilePrepper.CLI -c Release
dotnet tool install -g FilePrepper.CLI --add-source src/FilePrepper.CLI/nupkg
```

## User Installation

### Global Install (Recommended)
```bash
dotnet tool install -g FilePrepper.CLI
```

### Update
```bash
dotnet tool update -g FilePrepper.CLI
```

### Uninstall
```bash
dotnet tool uninstall -g FilePrepper.CLI
```

### List Installed Tools
```bash
dotnet tool list -g
```

## Troubleshooting

### "Tool already installed"
```bash
# Update instead
dotnet tool update -g FilePrepper.CLI

# Or uninstall first
dotnet tool uninstall -g FilePrepper.CLI
dotnet tool install -g FilePrepper.CLI
```

### "Command not found after install"
```bash
# Check PATH
echo $PATH  # Linux/Mac
echo %PATH% # Windows

# Tool installed to:
# Windows: %USERPROFILE%\.dotnet\tools
# Linux/Mac: ~/.dotnet/tools
```

### Package not found
```bash
# Clear NuGet cache
dotnet nuget locals all --clear

# Try again
dotnet tool install -g FilePrepper.CLI
```

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Publish Tool

on:
  release:
    types: [published]

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'

      - name: Build
        run: dotnet pack src/FilePrepper.CLI -c Release

      - name: Publish to NuGet
        run: |
          dotnet nuget push src/FilePrepper.CLI/nupkg/*.nupkg \
            --api-key ${{ secrets.NUGET_API_KEY }} \
            --source https://api.nuget.org/v3/index.json
```

## Best Practices

1. **Test before publishing** - Always test locally first
2. **Version appropriately** - Follow semantic versioning
3. **Document changes** - Maintain CHANGELOG.md
4. **Tag releases** - Use Git tags for versions
5. **Keep dependencies updated** - Regular security updates
6. **Monitor downloads** - Check NuGet statistics
7. **Respond to issues** - Engage with users

## Package Metadata

Current configuration in `FilePrepper.CLI.csproj`:

```xml
<PackageId>FilePrepper.CLI</PackageId>
<Version>0.3.0</Version>
<Authors>FilePrepper Contributors</Authors>
<Description>A powerful .NET CLI tool for CSV/tabular data processing</Description>
<PackageTags>csv;data-processing;etl;cli;preprocessing;ml;dotnet-tool</PackageTags>
<PackageLicenseExpression>MIT</PackageLicenseExpression>
<ToolCommandName>fileprepper</ToolCommandName>
```

## Next Steps

After first publish:
1. Announce on social media
2. Submit to .NET tool lists
3. Create demo video
4. Write blog post
5. Monitor feedback
