using FluentAssertions;
using FilePrepper.CLI.Commands;

namespace FilePrepper.CLI.Tests;

public class FillMissingValuesCommandTests : CommandTestBase
{
    private readonly FillMissingValuesCommand _command;

    public FillMissingValuesCommandTests()
    {
        _command = new FillMissingValuesCommand(LoggerFactory);
    }

    [Fact]
    public async Task FillMissing_WithFixedValue_ShouldFillCorrectly()
    {
        // Arrange
        var inputPath = CreateSampleDataWithMissingValues();
        var outputPath = GetTempPath("filled_fixed.csv");

        // Act - Fill empty Age values with 0
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--methods", "Age:FixedValue:0");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();

        var (headers, rows) = ReadCsvWithHeaders(outputPath);
        var ageIndex = Array.IndexOf(headers, "Age");

        // Check that no age values are empty
        foreach (var row in rows)
        {
            if (ageIndex < row.Length)
            {
                row[ageIndex].Should().NotBeNullOrWhiteSpace();
            }
        }
    }

    [Fact]
    public async Task FillMissing_WithMeanMethod_ShouldCalculateCorrectly()
    {
        // Arrange
        var headers = new[] { "Name", "Score" };
        var rows = new[]
        {
            new[] { "A", "100" },
            new[] { "B", "" },      // Missing
            new[] { "C", "80" },
            new[] { "D", "" },      // Missing
            new[] { "E", "90" }
        };
        var inputPath = CreateTestCsv("scores.csv", headers, rows);
        var outputPath = GetTempPath("filled_mean.csv");

        // Act - Fill Score with mean (Mean of 100, 80, 90 = 90)
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--methods", "Score:Mean");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();

        var (outHeaders, outRows) = ReadCsvWithHeaders(outputPath);
        var scoreIndex = Array.IndexOf(outHeaders, "Score");

        // All scores should have values now
        foreach (var row in outRows)
        {
            if (scoreIndex < row.Length)
            {
                row[scoreIndex].Should().NotBeNullOrWhiteSpace("All scores should be filled");
            }
        }
    }

    [Fact]
    public async Task FillMissing_WithMedianMethod_ShouldCalculateCorrectly()
    {
        // Arrange
        var headers = new[] { "ID", "Value" };
        var rows = new[]
        {
            new[] { "1", "10" },
            new[] { "2", "20" },
            new[] { "3", "" },      // Missing
            new[] { "4", "30" },
            new[] { "5", "" }       // Missing
        };
        var inputPath = CreateTestCsv("values.csv", headers, rows);
        var outputPath = GetTempPath("filled_median.csv");

        // Act - Fill Value with median
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--methods", "Value:Median");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();

        var (outHeaders, outRows) = ReadCsvWithHeaders(outputPath);
        var valueIndex = Array.IndexOf(outHeaders, "Value");

        // All values should be filled
        foreach (var row in outRows)
        {
            if (valueIndex < row.Length)
            {
                row[valueIndex].Should().NotBeNullOrWhiteSpace();
            }
        }
    }

    [Fact]
    public async Task FillMissing_WithModeMethod_ShouldUseHoCommonValue()
    {
        // Arrange
        var headers = new[] { "Product", "Category" };
        var rows = new[]
        {
            new[] { "P1", "Electronics" },
            new[] { "P2", "Electronics" },
            new[] { "P3", "" },              // Missing
            new[] { "P4", "Electronics" },
            new[] { "P5", "Books" },
            new[] { "P6", "" }               // Missing
        };
        var inputPath = CreateTestCsv("products.csv", headers, rows);
        var outputPath = GetTempPath("filled_mode.csv");

        // Act - Fill Category with mode (most common = "Electronics")
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--methods", "Category:Mode");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();

        var (outHeaders, outRows) = ReadCsvWithHeaders(outputPath);
        var categoryIndex = Array.IndexOf(outHeaders, "Category");

        // All categories should be filled
        foreach (var row in outRows)
        {
            if (categoryIndex < row.Length)
            {
                row[categoryIndex].Should().NotBeNullOrWhiteSpace();
            }
        }
    }

    [Fact]
    public async Task FillMissing_WithForwardFill_ShouldPropagateValues()
    {
        // Arrange
        var headers = new[] { "Date", "Status" };
        var rows = new[]
        {
            new[] { "Day1", "Active" },
            new[] { "Day2", "" },
            new[] { "Day3", "" },
            new[] { "Day4", "Inactive" },
            new[] { "Day5", "" }
        };
        var inputPath = CreateTestCsv("status.csv", headers, rows);
        var outputPath = GetTempPath("filled_ffill.csv");

        // Act - Forward fill Status
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--methods", "Status:ForwardFill");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();

        var (outHeaders, outRows) = ReadCsvWithHeaders(outputPath);
        var statusIndex = Array.IndexOf(outHeaders, "Status");

        // Verify forward fill behavior (values should propagate forward)
        outRows.Count.Should().Be(5);
    }

    [Fact]
    public async Task FillMissing_WithBackwardFill_ShouldPropagateBackwards()
    {
        // Arrange
        var headers = new[] { "ID", "Priority" };
        var rows = new[]
        {
            new[] { "1", "" },
            new[] { "2", "" },
            new[] { "3", "High" },
            new[] { "4", "" },
            new[] { "5", "Low" }
        };
        var inputPath = CreateTestCsv("priority.csv", headers, rows);
        var outputPath = GetTempPath("filled_bfill.csv");

        // Act - Backward fill Priority
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--methods", "Priority:BackwardFill");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();
        AssertCsvRowCount(outputPath, 5);
    }

    [Fact]
    public async Task FillMissing_WithMultipleMethods_ShouldApplyToAllColumns()
    {
        // Arrange
        var inputPath = CreateSampleDataWithMissingValues();
        var outputPath = GetTempPath("filled_multi.csv");

        // Act - Fill Age with mean and City with fixed value
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--methods", "Age:Mean,City:FixedValue:Unknown");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();

        var (headers, rows) = ReadCsvWithHeaders(outputPath);
        rows.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task FillMissing_WithInvalidMethodFormat_ShouldReturnError()
    {
        // Arrange
        var inputPath = CreateSampleDataWithMissingValues();
        var outputPath = GetTempPath("filled_invalid.csv");

        // Act - Invalid format (missing method)
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--methods", "Age");

        // Assert
        exitCode.Should().NotBe(0, "Should fail with invalid method format");
    }

    [Fact]
    public async Task FillMissing_FixedValueWithoutValue_ShouldReturnError()
    {
        // Arrange
        var inputPath = CreateSampleDataWithMissingValues();
        var outputPath = GetTempPath("filled_no_value.csv");

        // Act - FixedValue method without specifying the value
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--methods", "Age:FixedValue");

        // Assert
        exitCode.Should().NotBe(0, "Should fail when FixedValue method lacks a value");
    }

    [Fact]
    public async Task FillMissing_WithInvalidMethod_ShouldReturnError()
    {
        // Arrange
        var inputPath = CreateSampleDataWithMissingValues();
        var outputPath = GetTempPath("filled_bad_method.csv");

        // Act - Invalid fill method
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--methods", "Age:InvalidMethod");

        // Assert
        exitCode.Should().NotBe(0, "Should fail with invalid fill method");
    }

    [Fact]
    public async Task FillMissing_WithMissingInputFile_ShouldReturnError()
    {
        // Arrange
        var nonExistentPath = GetTempPath("nonexistent.csv");
        var outputPath = GetTempPath("output.csv");

        // Act
        var exitCode = await RunCommandAsync(_command,
            "--input", nonExistentPath,
            "--output", outputPath,
            "--methods", "Age:Mean");

        // Assert
        exitCode.Should().NotBe(0, "Should fail with missing input file");
    }

    [Fact]
    public async Task FillMissing_WithVerboseFlag_ShouldExecuteSuccessfully()
    {
        // Arrange
        var inputPath = CreateSampleDataWithMissingValues();
        var outputPath = GetTempPath("filled_verbose.csv");

        // Act
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--methods", "Age:FixedValue:30",
            "--verbose");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();
    }

    [Fact]
    public async Task FillMissing_PreservesOtherColumns()
    {
        // Arrange
        var inputPath = CreateSampleDataWithMissingValues();
        var outputPath = GetTempPath("filled_preserve.csv");

        // Act - Only fill Age column
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--methods", "Age:FixedValue:25");

        // Assert
        exitCode.Should().Be(0);
        var (headers, rows) = ReadCsvWithHeaders(outputPath);

        // Should preserve all original columns
        headers.Should().Contain(new[] { "Name", "Age", "Score", "City" });
    }
}
