using FluentAssertions;
using FilePrepper.CLI.Commands;

namespace FilePrepper.CLI.Tests;

public class BasicStatisticsCommandTests : CommandTestBase
{
    private readonly BasicStatisticsCommand _command;

    public BasicStatisticsCommandTests()
    {
        _command = new BasicStatisticsCommand(LoggerFactory);
    }

    [Fact]
    public async Task Stats_WithMeanCalculation_ShouldCalculateCorrectly()
    {
        // Arrange
        var headers = new[] { "Name", "Score" };
        var rows = new[]
        {
            new[] { "Alice", "80" },
            new[] { "Bob", "90" },
            new[] { "Charlie", "100" }
        };
        var inputPath = CreateTestCsv("scores.csv", headers, rows);
        var outputPath = GetTempPath("stats_mean.csv");

        // Act - Calculate mean for Score column
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--columns", "Score",
            "--stats", "Mean");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();

        var (outHeaders, outRows) = ReadCsvWithHeaders(outputPath);
        outHeaders.Should().Contain("Score_stat");
        outRows.Count.Should().Be(3);
    }

    [Fact]
    public async Task Stats_WithMultipleStatistics_ShouldCalculateAll()
    {
        // Arrange
        var headers = new[] { "ID", "Value" };
        var rows = new[]
        {
            new[] { "1", "10" },
            new[] { "2", "20" },
            new[] { "3", "30" },
            new[] { "4", "40" },
            new[] { "5", "50" }
        };
        var inputPath = CreateTestCsv("data.csv", headers, rows);
        var outputPath = GetTempPath("stats_multi.csv");

        // Act - Calculate Mean, Min, Max, Median
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--columns", "Value",
            "--stats", "Mean,Min,Max,Median");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();

        // Should have original columns plus statistics columns
        AssertCsvRowCount(outputPath, 5);
    }

    [Fact]
    public async Task Stats_WithStandardDeviation_ShouldCalculateCorrectly()
    {
        // Arrange
        var headers = new[] { "Sample", "Measurement" };
        var rows = new[]
        {
            new[] { "S1", "100" },
            new[] { "S2", "110" },
            new[] { "S3", "90" },
            new[] { "S4", "105" },
            new[] { "S5", "95" }
        };
        var inputPath = CreateTestCsv("measurements.csv", headers, rows);
        var outputPath = GetTempPath("stats_std.csv");

        // Act - Calculate StandardDeviation
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--columns", "Measurement",
            "--stats", "StandardDeviation");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();
        AssertCsvHasHeader(outputPath, "Measurement_stat");
    }

    [Fact]
    public async Task Stats_WithMinMax_ShouldCalculateCorrectly()
    {
        // Arrange
        var headers = new[] { "Product", "Price" };
        var rows = new[]
        {
            new[] { "A", "19.99" },
            new[] { "B", "29.99" },
            new[] { "C", "9.99" }
        };
        var inputPath = CreateTestCsv("prices.csv", headers, rows);
        var outputPath = GetTempPath("stats_minmax.csv");

        // Act - Calculate Min and Max
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--columns", "Price",
            "--stats", "Min,Max");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();
        AssertCsvRowCount(outputPath, 3);
    }

    [Fact]
    public async Task Stats_WithMedian_ShouldCalculateCorrectly()
    {
        // Arrange
        var headers = new[] { "ID", "Age" };
        var rows = new[]
        {
            new[] { "1", "25" },
            new[] { "2", "30" },
            new[] { "3", "35" },
            new[] { "4", "40" },
            new[] { "5", "45" }
        };
        var inputPath = CreateTestCsv("ages.csv", headers, rows);
        var outputPath = GetTempPath("stats_median.csv");

        // Act - Calculate Median
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--columns", "Age",
            "--stats", "Median");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();
        AssertCsvHasHeader(outputPath, "Age_stat");
    }

    [Fact]
    public async Task Stats_WithQuartiles_ShouldCalculateCorrectly()
    {
        // Arrange
        var headers = new[] { "Value" };
        var rows = new[]
        {
            new[] { "10" }, new[] { "20" }, new[] { "30" },
            new[] { "40" }, new[] { "50" }, new[] { "60" },
            new[] { "70" }, new[] { "80" }, new[] { "90" }
        };
        var inputPath = CreateTestCsv("quartiles.csv", headers, rows);
        var outputPath = GetTempPath("stats_quartiles.csv");

        // Act - Calculate Q1 and Q3
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--columns", "Value",
            "--stats", "Q1,Q3");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();
    }

    [Fact]
    public async Task Stats_WithMultipleColumns_ShouldCalculateForAll()
    {
        // Arrange
        var headers = new[] { "Name", "Math", "Science" };
        var rows = new[]
        {
            new[] { "Alice", "85", "90" },
            new[] { "Bob", "90", "85" },
            new[] { "Charlie", "95", "95" }
        };
        var inputPath = CreateTestCsv("grades.csv", headers, rows);
        var outputPath = GetTempPath("stats_multi_col.csv");

        // Act - Calculate mean for both Math and Science
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--columns", "Math,Science",
            "--stats", "Mean");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();

        var (outHeaders, _) = ReadCsvWithHeaders(outputPath);
        // Should have stat columns for both Math and Science
        outHeaders.Should().Contain("Math_stat");
        outHeaders.Should().Contain("Science_stat");
    }

    [Fact]
    public async Task Stats_WithCustomSuffix_ShouldUseProvidedSuffix()
    {
        // Arrange
        var headers = new[] { "Value" };
        var rows = new[] { new[] { "100" }, new[] { "200" } };
        var inputPath = CreateTestCsv("values.csv", headers, rows);
        var outputPath = GetTempPath("stats_suffix.csv");

        // Act - Use custom suffix "_avg"
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--columns", "Value",
            "--stats", "Mean",
            "--suffix", "_avg");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();
        AssertCsvHasHeader(outputPath, "Value_avg");
    }

    [Fact]
    public async Task Stats_WithInvalidStatistic_ShouldReturnError()
    {
        // Arrange
        var inputPath = CreateSampleSalesData();
        var outputPath = GetTempPath("stats_invalid.csv");

        // Act - Invalid statistic name
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--columns", "Price",
            "--stats", "InvalidStat");

        // Assert
        exitCode.Should().NotBe(0, "Should fail with invalid statistic");
    }

    [Fact]
    public async Task Stats_WithMissingInputFile_ShouldReturnError()
    {
        // Arrange
        var nonExistentPath = GetTempPath("nonexistent.csv");
        var outputPath = GetTempPath("output.csv");

        // Act
        var exitCode = await RunCommandAsync(_command,
            "--input", nonExistentPath,
            "--output", outputPath,
            "--columns", "Value",
            "--stats", "Mean");

        // Assert
        exitCode.Should().NotBe(0, "Should fail with missing input file");
    }

    [Fact]
    public async Task Stats_WithVerboseFlag_ShouldExecuteSuccessfully()
    {
        // Arrange
        var inputPath = CreateSampleSalesData();
        var outputPath = GetTempPath("stats_verbose.csv");

        // Act
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--columns", "Quantity",
            "--stats", "Mean",
            "--verbose");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();
    }

    [Fact]
    public async Task Stats_PreservesOriginalColumns()
    {
        // Arrange
        var headers = new[] { "ID", "Name", "Score" };
        var rows = new[]
        {
            new[] { "1", "Alice", "90" },
            new[] { "2", "Bob", "85" }
        };
        var inputPath = CreateTestCsv("preserve.csv", headers, rows);
        var outputPath = GetTempPath("stats_preserve.csv");

        // Act
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--columns", "Score",
            "--stats", "Mean");

        // Assert
        exitCode.Should().Be(0);
        var (outHeaders, _) = ReadCsvWithHeaders(outputPath);

        // Should preserve original columns
        outHeaders.Should().Contain(new[] { "ID", "Name", "Score" });
    }

    [Fact]
    public async Task Stats_WithZScore_ShouldCalculateCorrectly()
    {
        // Arrange
        var headers = new[] { "Value" };
        var rows = new[]
        {
            new[] { "100" },
            new[] { "110" },
            new[] { "120" },
            new[] { "130" },
            new[] { "140" }
        };
        var inputPath = CreateTestCsv("zscore.csv", headers, rows);
        var outputPath = GetTempPath("stats_zscore.csv");

        // Act - Calculate ZScore
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--columns", "Value",
            "--stats", "ZScore");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();
        AssertCsvHasHeader(outputPath, "Value_stat");
    }

    [Fact]
    public async Task Stats_WithDefaultValue_ShouldHandleErrors()
    {
        // Arrange
        var headers = new[] { "ID", "Value" };
        var rows = new[]
        {
            new[] { "1", "100" },
            new[] { "2", "invalid" },  // Invalid numeric value
            new[] { "3", "200" }
        };
        var inputPath = CreateTestCsv("errors.csv", headers, rows);
        var outputPath = GetTempPath("stats_default.csv");

        // Act - Use default value for errors
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--columns", "Value",
            "--stats", "Mean",
            "--default-value", "0");

        // Assert - Should handle gracefully with default value
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();
    }
}
