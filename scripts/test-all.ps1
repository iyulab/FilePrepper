#!/usr/bin/env pwsh
# FilePrepper - Build and Test All Script
# Performs clean build and runs all tests with summary

param(
    [switch]$SkipClean,
    [switch]$Verbose,
    [switch]$Coverage
)

# Color functions
function Write-Success { Write-Host $args -ForegroundColor Green }
function Write-Error { Write-Host $args -ForegroundColor Red }
function Write-Info { Write-Host $args -ForegroundColor Cyan }
function Write-Warning { Write-Host $args -ForegroundColor Yellow }

# Banner
Write-Host "`n╔═══════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║       FilePrepper - Build & Test Suite                   ║" -ForegroundColor Cyan
Write-Host "╚═══════════════════════════════════════════════════════════╝`n" -ForegroundColor Cyan

# Start timer
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

# Save current location
$originalLocation = Get-Location
$scriptRoot = Split-Path -Parent $PSScriptRoot
$srcPath = Join-Path $scriptRoot "src"

# Verify src directory exists
if (!(Test-Path $srcPath)) {
    Write-Error "❌ Error: src directory not found at $srcPath"
    exit 1
}

Set-Location $srcPath

# Initialize result tracking
$buildSuccess = $false
$testSuccess = $false
$totalTests = 0
$passedTests = 0
$failedTests = 0
$skippedTests = 0
$warnings = 0
$errors = 0

try {
    # Step 1: Clean (unless skipped)
    if (!$SkipClean) {
        Write-Info "🧹 Step 1/4: Cleaning previous builds..."
        $cleanOutput = dotnet clean 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Success "✓ Clean completed"
        } else {
            Write-Warning "⚠ Clean completed with warnings"
        }
    } else {
        Write-Info "⏭️  Skipping clean step"
    }

    # Step 2: Restore
    Write-Info "`n📦 Step 2/4: Restoring packages..."
    $restoreOutput = dotnet restore 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Success "✓ Restore completed"
    } else {
        Write-Error "❌ Restore failed"
        exit 1
    }

    # Step 3: Build
    Write-Info "`n🔨 Step 3/4: Building solution..."
    $buildOutput = dotnet build --no-restore 2>&1 | Out-String

    # Parse build output for warnings and errors
    $buildOutput -split "`n" | ForEach-Object {
        if ($_ -match '(\d+) Warning\(s\)') { $warnings = [int]$matches[1] }
        if ($_ -match '(\d+) Error\(s\)') { $errors = [int]$matches[1] }
    }

    if ($LASTEXITCODE -eq 0) {
        $buildSuccess = $true
        Write-Success "✓ Build successful"
        if ($warnings -gt 0) {
            Write-Warning "  ⚠ $warnings warning(s) found"
        }
    } else {
        Write-Error "❌ Build failed with $errors error(s)"
        if ($Verbose) {
            Write-Host "`nBuild output:" -ForegroundColor Yellow
            Write-Host $buildOutput
        }
        exit 1
    }

    # Step 4: Test
    Write-Info "`n🧪 Step 4/4: Running tests..."

    $testArgs = @('test', '--no-build', '--verbosity', 'normal')

    if ($Coverage) {
        $testArgs += '/p:CollectCoverage=true'
        $testArgs += '/p:CoverletOutputFormat=cobertura'
    }

    $testOutput = dotnet @testArgs 2>&1 | Out-String

    # Parse test output
    $testOutput -split "`n" | ForEach-Object {
        if ($_ -match 'Total tests:\s+(\d+)') {
            $totalTests = [int]$matches[1]
        }
        if ($_ -match '^\s+Passed:\s+(\d+)') {
            $passedTests = [int]$matches[1]
        }
        if ($_ -match '^\s+Failed:\s+(\d+)') {
            $failedTests = [int]$matches[1]
        }
        if ($_ -match '^\s+Skipped:\s+(\d+)') {
            $skippedTests = [int]$matches[1]
        }
    }

    if ($LASTEXITCODE -eq 0) {
        $testSuccess = $true
        Write-Success "✓ All tests passed"
    } else {
        Write-Error "❌ Some tests failed"
        if ($Verbose) {
            Write-Host "`nTest output:" -ForegroundColor Yellow
            Write-Host $testOutput
        }
    }

} finally {
    # Return to original location
    Set-Location $originalLocation
}

# Stop timer
$stopwatch.Stop()
$duration = $stopwatch.Elapsed

# Summary Report
Write-Host "`n╔═══════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║                    SUMMARY REPORT                         ║" -ForegroundColor Cyan
Write-Host "╚═══════════════════════════════════════════════════════════╝`n" -ForegroundColor Cyan

Write-Host "📊 Build Results:" -ForegroundColor White
if ($buildSuccess) {
    Write-Success "   ✓ Status: SUCCESS"
} else {
    Write-Error "   ✗ Status: FAILED"
}
Write-Host "   • Warnings: $warnings"
Write-Host "   • Errors: $errors"

Write-Host "`n🧪 Test Results:" -ForegroundColor White
if ($testSuccess) {
    Write-Success "   ✓ Status: SUCCESS"
} else {
    Write-Error "   ✗ Status: FAILED"
}
Write-Host "   • Total:   $totalTests tests"
Write-Success "   • Passed:  $passedTests tests"
if ($failedTests -gt 0) {
    Write-Error "   • Failed:  $failedTests tests"
} else {
    Write-Host "   • Failed:  $failedTests tests"
}
if ($skippedTests -gt 0) {
    Write-Warning "   • Skipped: $skippedTests tests"
}

# Calculate pass rate
if ($totalTests -gt 0) {
    $passRate = [math]::Round(($passedTests / $totalTests) * 100, 1)
    Write-Host "   • Pass Rate: $passRate%"
}

Write-Host "`n⏱️  Total Duration: $($duration.ToString('mm\:ss\.fff'))" -ForegroundColor White

# Coverage info if enabled
if ($Coverage) {
    Write-Host "`n📈 Coverage report generated in TestResults/" -ForegroundColor Cyan
}

# Final Status
Write-Host "`n" -NoNewline
if ($buildSuccess -and $testSuccess) {
    Write-Success "════════════════════════════════════════════════════════════"
    Write-Success "  🎉 ALL CHECKS PASSED - Ready for deployment!"
    Write-Success "════════════════════════════════════════════════════════════"
    exit 0
} else {
    Write-Error "════════════════════════════════════════════════════════════"
    Write-Error "  ❌ CHECKS FAILED - Review errors above"
    Write-Error "════════════════════════════════════════════════════════════"
    exit 1
}
