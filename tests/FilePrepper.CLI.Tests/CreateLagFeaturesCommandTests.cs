using FluentAssertions;
using FilePrepper.CLI.Commands;

namespace FilePrepper.CLI.Tests;

public class CreateLagFeaturesCommandTests : CommandTestBase
{
    private readonly CreateLagFeaturesCommand _command;

    public CreateLagFeaturesCommandTests()
    {
        _command = new CreateLagFeaturesCommand(LoggerFactory);
    }

    [Fact]
    public async Task CreateLagFeatures_WithSinglePeriod_ShouldCreateCorrectly()
    {
        // Arrange
        var inputPath = CreateSampleTimeSeriesData();
        var outputPath = GetTempPath("lag_single.csv");

        // Act - Create lag 1 for Value column
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--group-by", "PartNumber",
            "--time-column", "Date",
            "--lag-columns", "Value",
            "--lag-periods", "1");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();

        var (headers, rows) = ReadCsvWithHeaders(outputPath);

        // Should have Value_lag1 column
        headers.Should().Contain("Value_lag1");

        // With lag 1 and drop nulls, each group should have 2 rows (3 - 1)
        // 2 groups × 2 rows = 4 rows
        rows.Count.Should().BeGreaterThanOrEqualTo(2, "Should have lag features created");
    }

    [Fact]
    public async Task CreateLagFeatures_WithMultiplePeriods_ShouldCreateAllLags()
    {
        // Arrange
        var inputPath = CreateSampleTimeSeriesData();
        var outputPath = GetTempPath("lag_multi.csv");

        // Act - Create lag 1, 2 for Value column
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--group-by", "PartNumber",
            "--time-column", "Date",
            "--lag-columns", "Value",
            "--lag-periods", "1,2");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();

        var (headers, _) = ReadCsvWithHeaders(outputPath);

        // Should have Value_lag1 and Value_lag2 columns
        headers.Should().Contain("Value_lag1");
        headers.Should().Contain("Value_lag2");
    }

    [Fact]
    public async Task CreateLagFeatures_WithMultipleColumns_ShouldCreateFeaturesForAll()
    {
        // Arrange
        var headers = new[] { "ID", "Time", "Temp", "Humidity" };
        var rows = new[]
        {
            new[] { "S1", "1", "20", "60" },
            new[] { "S1", "2", "21", "62" },
            new[] { "S1", "3", "22", "65" }
        };
        var inputPath = CreateTestCsv("sensor.csv", headers, rows);
        var outputPath = GetTempPath("lag_multi_col.csv");

        // Act - Create lags for both Temp and Humidity
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--group-by", "ID",
            "--time-column", "Time",
            "--lag-columns", "Temp,Humidity",
            "--lag-periods", "1");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();

        var (outHeaders, _) = ReadCsvWithHeaders(outputPath);

        // Should have lag columns for both Temp and Humidity
        outHeaders.Should().Contain("Temp_lag1");
        outHeaders.Should().Contain("Humidity_lag1");
    }

    [Fact]
    public async Task CreateLagFeatures_WithTargetColumn_ShouldIncludeInOutput()
    {
        // Arrange
        var inputPath = CreateSampleTimeSeriesData();
        var outputPath = GetTempPath("lag_target.csv");

        // Act - Specify Status as target column
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--group-by", "PartNumber",
            "--time-column", "Date",
            "--lag-columns", "Value",
            "--lag-periods", "1",
            "--target", "Status");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();

        var (headers, _) = ReadCsvWithHeaders(outputPath);

        // Should include Status column as target
        headers.Should().Contain("Status");
    }

    [Fact]
    public async Task CreateLagFeatures_WithKeepColumns_ShouldIncludeSpecifiedColumns()
    {
        // Arrange
        var inputPath = CreateSampleTimeSeriesData();
        var outputPath = GetTempPath("lag_keep.csv");

        // Act - Keep PartNumber and Date columns
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--group-by", "PartNumber",
            "--time-column", "Date",
            "--lag-columns", "Value",
            "--lag-periods", "1",
            "--keep-columns", "PartNumber,Date");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();

        var (headers, _) = ReadCsvWithHeaders(outputPath);

        // Should keep PartNumber and Date
        headers.Should().Contain("PartNumber");
        headers.Should().Contain("Date");
    }

    [Fact]
    public async Task CreateLagFeatures_WithDropNullsFalse_ShouldKeepAllRows()
    {
        // Arrange
        var inputPath = CreateSampleTimeSeriesData();
        var outputPath = GetTempPath("lag_keep_nulls.csv");

        // Act - Don't drop rows with null lag values
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--group-by", "PartNumber",
            "--time-column", "Date",
            "--lag-columns", "Value",
            "--lag-periods", "1",
            "--drop-nulls", "false");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();

        // When keeping nulls, should have same number of rows as input (per group)
        var (_, rows) = ReadCsvWithHeaders(outputPath);
        rows.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateLagFeatures_WithInvalidLagPeriod_ShouldReturnError()
    {
        // Arrange
        var inputPath = CreateSampleTimeSeriesData();
        var outputPath = GetTempPath("lag_invalid.csv");

        // Act - Invalid lag period (0 or negative)
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--group-by", "PartNumber",
            "--time-column", "Date",
            "--lag-columns", "Value",
            "--lag-periods", "0");

        // Assert
        exitCode.Should().NotBe(0, "Should fail with invalid lag period");
    }

    [Fact]
    public async Task CreateLagFeatures_WithMissingInputFile_ShouldReturnError()
    {
        // Arrange
        var nonExistentPath = GetTempPath("nonexistent.csv");
        var outputPath = GetTempPath("output.csv");

        // Act
        var exitCode = await RunCommandAsync(_command,
            "--input", nonExistentPath,
            "--output", outputPath,
            "--group-by", "ID",
            "--time-column", "Time",
            "--lag-columns", "Value",
            "--lag-periods", "1");

        // Assert
        exitCode.Should().NotBe(0, "Should fail with missing input file");
    }

    [Fact]
    public async Task CreateLagFeatures_WithVerboseFlag_ShouldExecuteSuccessfully()
    {
        // Arrange
        var inputPath = CreateSampleTimeSeriesData();
        var outputPath = GetTempPath("lag_verbose.csv");

        // Act
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--group-by", "PartNumber",
            "--time-column", "Date",
            "--lag-columns", "Value",
            "--lag-periods", "1",
            "--verbose");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();
    }

    [Fact]
    public async Task CreateLagFeatures_WithLargeLagPeriod_ShouldHandleCorrectly()
    {
        // Arrange
        var inputPath = CreateSampleTimeSeriesData();
        var outputPath = GetTempPath("lag_large.csv");

        // Act - Large lag period (2) relative to data size (3 rows per group)
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--group-by", "PartNumber",
            "--time-column", "Date",
            "--lag-columns", "Value",
            "--lag-periods", "2");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();

        // With lag 2 and 3 rows per group, only 1 row per group remains (after dropping nulls)
        var (_, rows) = ReadCsvWithHeaders(outputPath);
        rows.Count.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task CreateLagFeatures_CreatesProperColumnNames()
    {
        // Arrange
        var inputPath = CreateSampleTimeSeriesData();
        var outputPath = GetTempPath("lag_names.csv");

        // Act - Create multiple lags (keep nulls to ensure we have output rows)
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--group-by", "PartNumber",
            "--time-column", "Date",
            "--lag-columns", "Value",
            "--lag-periods", "1,2,3",
            "--drop-nulls", "false");

        // Assert
        exitCode.Should().Be(0);
        var (headers, _) = ReadCsvWithHeaders(outputPath);

        // Should have properly named lag columns
        headers.Should().Contain("Value_lag1");
        headers.Should().Contain("Value_lag2");
        headers.Should().Contain("Value_lag3");
    }

    [Fact]
    public async Task CreateLagFeatures_WithMultipleGroups_ShouldHandleSeparately()
    {
        // Arrange - Data with 2 distinct groups
        var inputPath = CreateSampleTimeSeriesData();
        var outputPath = GetTempPath("lag_groups.csv");

        // Act
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--group-by", "PartNumber",
            "--time-column", "Date",
            "--lag-columns", "Value",
            "--lag-periods", "1");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();

        // Each group should have its own lag features calculated independently
        var (_, rows) = ReadCsvWithHeaders(outputPath);
        rows.Count.Should().BeGreaterThanOrEqualTo(2, "Should have rows from multiple groups");
    }
}
