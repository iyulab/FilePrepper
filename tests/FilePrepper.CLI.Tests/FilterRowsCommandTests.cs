using FluentAssertions;
using FilePrepper.CLI.Commands;

namespace FilePrepper.CLI.Tests;

public class FilterRowsCommandTests : CommandTestBase
{
    private readonly FilterRowsCommand _command;

    public FilterRowsCommandTests()
    {
        _command = new FilterRowsCommand(LoggerFactory);
    }

    [Fact]
    public async Task FilterRows_WithEqualsCondition_ShouldFilterCorrectly()
    {
        // Arrange
        var inputPath = CreateSampleSalesData();
        var outputPath = GetTempPath("filtered.csv");

        // Act - Filter for "Widget A" products
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--conditions", "Product:Equals:Widget A");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();

        var (headers, rows) = ReadCsvWithHeaders(outputPath);
        rows.Count.Should().Be(2, "Should have 2 Widget A entries");

        foreach (var row in rows)
        {
            var productIndex = Array.IndexOf(headers, "Product");
            row[productIndex].Should().Be("Widget A");
        }
    }

    [Fact]
    public async Task FilterRows_WithGreaterThanCondition_ShouldFilterCorrectly()
    {
        // Arrange
        var inputPath = CreateSampleSalesData();
        var outputPath = GetTempPath("filtered_gt.csv");

        // Act - Filter for Quantity > 10
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--conditions", "Quantity:GreaterThan:10");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();

        var (headers, rows) = ReadCsvWithHeaders(outputPath);
        rows.Count.Should().Be(2, "Should have 2 rows with Quantity > 10");

        var quantityIndex = Array.IndexOf(headers, "Quantity");
        foreach (var row in rows)
        {
            int.Parse(row[quantityIndex]).Should().BeGreaterThan(10);
        }
    }

    [Fact]
    public async Task FilterRows_WithMultipleConditions_ShouldApplyAllFilters()
    {
        // Arrange
        var inputPath = CreateSampleSalesData();
        var outputPath = GetTempPath("filtered_multi.csv");

        // Act - Filter for Region = "North" AND Quantity > 5
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--conditions", "Region:Equals:North,Quantity:GreaterThan:5");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();

        var (headers, rows) = ReadCsvWithHeaders(outputPath);
        rows.Count.Should().Be(2, "Should have 2 rows matching both conditions");

        var regionIndex = Array.IndexOf(headers, "Region");
        var quantityIndex = Array.IndexOf(headers, "Quantity");

        foreach (var row in rows)
        {
            row[regionIndex].Should().Be("North");
            int.Parse(row[quantityIndex]).Should().BeGreaterThan(5);
        }
    }

    [Fact]
    public async Task FilterRows_WithContainsCondition_ShouldFilterCorrectly()
    {
        // Arrange
        var inputPath = CreateSampleSalesData();
        var outputPath = GetTempPath("filtered_contains.csv");

        // Act - Filter for Product containing "Widget"
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--conditions", "Product:Contains:Widget");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();

        var (headers, rows) = ReadCsvWithHeaders(outputPath);
        rows.Count.Should().Be(5, "All products contain 'Widget'");

        var productIndex = Array.IndexOf(headers, "Product");
        foreach (var row in rows)
        {
            row[productIndex].Should().Contain("Widget");
        }
    }

    [Fact]
    public async Task FilterRows_WithLessThanCondition_ShouldFilterCorrectly()
    {
        // Arrange
        var inputPath = CreateSampleSalesData();
        var outputPath = GetTempPath("filtered_lt.csv");

        // Act - Filter for Quantity < 10
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--conditions", "Quantity:LessThan:10");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();

        var (headers, rows) = ReadCsvWithHeaders(outputPath);
        rows.Count.Should().Be(2, "Should have 2 rows with Quantity < 10");

        var quantityIndex = Array.IndexOf(headers, "Quantity");
        foreach (var row in rows)
        {
            int.Parse(row[quantityIndex]).Should().BeLessThan(10);
        }
    }

    [Fact]
    public async Task FilterRows_WithNotEqualsCondition_ShouldFilterCorrectly()
    {
        // Arrange
        var inputPath = CreateSampleSalesData();
        var outputPath = GetTempPath("filtered_ne.csv");

        // Act - Filter for Region != "North"
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--conditions", "Region:NotEquals:North");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();

        var (headers, rows) = ReadCsvWithHeaders(outputPath);
        rows.Count.Should().Be(3, "Should have 3 rows where Region != North");

        var regionIndex = Array.IndexOf(headers, "Region");
        foreach (var row in rows)
        {
            row[regionIndex].Should().NotBe("North");
        }
    }

    [Fact]
    public async Task FilterRows_WithInvalidConditionFormat_ShouldReturnError()
    {
        // Arrange
        var inputPath = CreateSampleSalesData();
        var outputPath = GetTempPath("filtered_invalid.csv");

        // Act - Invalid condition format (missing colon)
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--conditions", "InvalidFormat");

        // Assert
        exitCode.Should().NotBe(0, "Should fail with invalid condition format");
    }

    [Fact]
    public async Task FilterRows_WithInvalidOperator_ShouldReturnError()
    {
        // Arrange
        var inputPath = CreateSampleSalesData();
        var outputPath = GetTempPath("filtered_bad_op.csv");

        // Act - Invalid operator
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--conditions", "Product:InvalidOp:Value");

        // Assert
        exitCode.Should().NotBe(0, "Should fail with invalid operator");
    }

    [Fact]
    public async Task FilterRows_WithMissingInputFile_ShouldReturnError()
    {
        // Arrange
        var nonExistentPath = GetTempPath("nonexistent.csv");
        var outputPath = GetTempPath("output.csv");

        // Act
        var exitCode = await RunCommandAsync(_command,
            "--input", nonExistentPath,
            "--output", outputPath,
            "--conditions", "Product:Equals:Value");

        // Assert
        exitCode.Should().NotBe(0, "Should fail with missing input file");
    }

    [Fact]
    public async Task FilterRows_WithStartsWithCondition_ShouldFilterCorrectly()
    {
        // Arrange
        var inputPath = CreateSampleSalesData();
        var outputPath = GetTempPath("filtered_starts.csv");

        // Act - Filter for Region starting with "N"
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--conditions", "Region:StartsWith:N");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();

        var (headers, rows) = ReadCsvWithHeaders(outputPath);
        rows.Count.Should().Be(2, "Should have 2 rows where Region starts with N");

        var regionIndex = Array.IndexOf(headers, "Region");
        foreach (var row in rows)
        {
            row[regionIndex].Should().StartWith("N");
        }
    }

    [Fact]
    public async Task FilterRows_WithVerboseFlag_ShouldExecuteSuccessfully()
    {
        // Arrange
        var inputPath = CreateSampleSalesData();
        var outputPath = GetTempPath("filtered_verbose.csv");

        // Act
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--conditions", "Product:Equals:Widget A",
            "--verbose");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();
        AssertCsvRowCount(outputPath, 2);
    }

    [Fact]
    public async Task FilterRows_PreservesHeaders_WhenHasHeaderIsTrue()
    {
        // Arrange
        var inputPath = CreateSampleSalesData();
        var outputPath = GetTempPath("filtered_headers.csv");

        // Act
        var exitCode = await RunCommandAsync(_command,
            "--input", inputPath,
            "--output", outputPath,
            "--conditions", "Product:Equals:Widget A",
            "--has-header");

        // Assert
        exitCode.Should().Be(0);
        var (headers, _) = ReadCsvWithHeaders(outputPath);
        headers.Should().Contain(new[] { "Date", "Product", "Quantity", "Price", "Region" });
    }
}
