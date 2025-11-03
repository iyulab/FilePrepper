using FluentAssertions;
using FilePrepper.CLI.Commands;

namespace FilePrepper.CLI.Tests;

public class MergeCommandTests : CommandTestBase
{
    private readonly MergeCommand _command;

    public MergeCommandTests()
    {
        _command = new MergeCommand(LoggerFactory);
    }

    [Fact]
    public async Task Merge_VerticalMerge_ShouldConcatenateRows()
    {
        // Arrange
        var file1 = CreateTestCsv("file1.csv", new[] { "Name", "Age" }, new[]
        {
            new[] { "Alice", "25" },
            new[] { "Bob", "30" }
        });

        var file2 = CreateTestCsv("file2.csv", new[] { "Name", "Age" }, new[]
        {
            new[] { "Charlie", "35" },
            new[] { "David", "40" }
        });

        var outputPath = GetTempPath("merged_vertical.csv");

        // Act
        var exitCode = await RunCommandAsync(_command,
            "--input", file1, file2,
            "--output", outputPath,
            "--type", "Vertical");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();

        var (headers, rows) = ReadCsvWithHeaders(outputPath);
        headers.Should().Contain(new[] { "Name", "Age" });
        rows.Count.Should().Be(4, "Should have 4 rows from 2 files");

        // Verify all names are present
        var names = rows.Select(r => r[0]).ToList();
        names.Should().Contain(new[] { "Alice", "Bob", "Charlie", "David" });
    }

    [Fact]
    public async Task Merge_HorizontalMerge_WithInnerJoin_ShouldJoinCorrectly()
    {
        // Arrange
        var (file1, file2) = CreateSampleMergeData();
        var outputPath = GetTempPath("merged_horizontal.csv");

        // Act - Inner join on ID column
        var exitCode = await RunCommandAsync(_command,
            "--input", file1, file2,
            "--output", outputPath,
            "--type", "Horizontal",
            "--join-type", "Inner",
            "--key-columns", "ID");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();

        var (headers, rows) = ReadCsvWithHeaders(outputPath);
        rows.Count.Should().Be(3, "Should have 3 rows from inner join");

        // Verify all expected columns are present
        headers.Should().Contain("ID");
        headers.Should().Contain("Name");
        headers.Should().Contain("Department");
        headers.Should().Contain("Salary");
        headers.Should().Contain("Years");
    }

    [Fact]
    public async Task Merge_HorizontalMerge_WithLeftJoin_ShouldIncludeAllLeftRows()
    {
        // Arrange
        var file1 = CreateTestCsv("left.csv", new[] { "ID", "Name" }, new[]
        {
            new[] { "1", "Alice" },
            new[] { "2", "Bob" },
            new[] { "3", "Charlie" }
        });

        var file2 = CreateTestCsv("right.csv", new[] { "ID", "Score" }, new[]
        {
            new[] { "1", "95" },
            new[] { "2", "88" }
            // ID 3 missing in right file
        });

        var outputPath = GetTempPath("merged_left.csv");

        // Act
        var exitCode = await RunCommandAsync(_command,
            "--input", file1, file2,
            "--output", outputPath,
            "--type", "Horizontal",
            "--join-type", "Left",
            "--key-columns", "ID");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();

        var (headers, rows) = ReadCsvWithHeaders(outputPath);
        rows.Count.Should().Be(3, "Left join should include all left rows");

        var idIndex = Array.IndexOf(headers, "ID");
        var ids = rows.Select(r => r[idIndex]).ToList();
        ids.Should().Contain(new[] { "1", "2", "3" });
    }

    [Fact]
    public async Task Merge_VerticalMerge_WithMultipleFiles_ShouldConcatenateAll()
    {
        // Arrange
        var file1 = CreateTestCsv("data1.csv", new[] { "X", "Y" }, new[]
        {
            new[] { "1", "A" }
        });

        var file2 = CreateTestCsv("data2.csv", new[] { "X", "Y" }, new[]
        {
            new[] { "2", "B" }
        });

        var file3 = CreateTestCsv("data3.csv", new[] { "X", "Y" }, new[]
        {
            new[] { "3", "C" }
        });

        var outputPath = GetTempPath("merged_multi.csv");

        // Act
        var exitCode = await RunCommandAsync(_command,
            "--input", file1, file2, file3,
            "--output", outputPath,
            "--type", "Vertical");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();
        AssertCsvRowCount(outputPath, 3);
    }

    [Fact]
    public async Task Merge_WithSingleFile_ShouldReturnError()
    {
        // Arrange
        var file1 = CreateSampleSalesData();
        var outputPath = GetTempPath("merged_single.csv");

        // Act - Only one file provided
        var exitCode = await RunCommandAsync(_command,
            "--input", file1,
            "--output", outputPath,
            "--type", "Vertical");

        // Assert
        exitCode.Should().NotBe(0, "Should fail with only one input file");
    }

    [Fact]
    public async Task Merge_HorizontalMerge_WithoutKeyColumns_ShouldReturnError()
    {
        // Arrange
        var (file1, file2) = CreateSampleMergeData();
        var outputPath = GetTempPath("merged_no_keys.csv");

        // Act - Horizontal merge without key columns
        var exitCode = await RunCommandAsync(_command,
            "--input", file1, file2,
            "--output", outputPath,
            "--type", "Horizontal");

        // Assert
        exitCode.Should().NotBe(0, "Should fail without key columns for horizontal merge");
    }

    [Fact]
    public async Task Merge_WithInvalidMergeType_ShouldReturnError()
    {
        // Arrange
        var (file1, file2) = CreateSampleMergeData();
        var outputPath = GetTempPath("merged_invalid.csv");

        // Act
        var exitCode = await RunCommandAsync(_command,
            "--input", file1, file2,
            "--output", outputPath,
            "--type", "InvalidType");

        // Assert
        exitCode.Should().NotBe(0, "Should fail with invalid merge type");
    }

    [Fact]
    public async Task Merge_WithInvalidJoinType_ShouldReturnError()
    {
        // Arrange
        var (file1, file2) = CreateSampleMergeData();
        var outputPath = GetTempPath("merged_invalid_join.csv");

        // Act
        var exitCode = await RunCommandAsync(_command,
            "--input", file1, file2,
            "--output", outputPath,
            "--type", "Horizontal",
            "--join-type", "InvalidJoin",
            "--key-columns", "ID");

        // Assert
        exitCode.Should().NotBe(0, "Should fail with invalid join type");
    }

    [Fact]
    public async Task Merge_VerticalMerge_WithVerboseFlag_ShouldExecuteSuccessfully()
    {
        // Arrange
        var file1 = CreateTestCsv("v1.csv", new[] { "Col1" }, new[] { new[] { "A" } });
        var file2 = CreateTestCsv("v2.csv", new[] { "Col1" }, new[] { new[] { "B" } });
        var outputPath = GetTempPath("merged_verbose.csv");

        // Act
        var exitCode = await RunCommandAsync(_command,
            "--input", file1, file2,
            "--output", outputPath,
            "--type", "Vertical",
            "--verbose");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();
        AssertCsvRowCount(outputPath, 2);
    }

    [Fact]
    public async Task Merge_HorizontalMerge_WithMultipleKeyColumns_ShouldJoinCorrectly()
    {
        // Arrange
        var file1 = CreateTestCsv("comp1.csv", new[] { "Year", "Quarter", "Sales" }, new[]
        {
            new[] { "2024", "Q1", "1000" },
            new[] { "2024", "Q2", "1200" }
        });

        var file2 = CreateTestCsv("comp2.csv", new[] { "Year", "Quarter", "Profit" }, new[]
        {
            new[] { "2024", "Q1", "200" },
            new[] { "2024", "Q2", "250" }
        });

        var outputPath = GetTempPath("merged_composite.csv");

        // Act - Join on Year and Quarter
        var exitCode = await RunCommandAsync(_command,
            "--input", file1, file2,
            "--output", outputPath,
            "--type", "Horizontal",
            "--join-type", "Inner",
            "--key-columns", "Year,Quarter");

        // Assert
        exitCode.Should().Be(0);
        FileExists(outputPath).Should().BeTrue();

        var (headers, rows) = ReadCsvWithHeaders(outputPath);
        rows.Count.Should().Be(2);
        headers.Should().Contain(new[] { "Year", "Quarter", "Sales", "Profit" });
    }

    [Fact]
    public async Task Merge_WithMissingInputFile_ShouldReturnError()
    {
        // Arrange
        var file1 = CreateSampleSalesData();
        var nonExistentFile = GetTempPath("nonexistent.csv");
        var outputPath = GetTempPath("merged_missing.csv");

        // Act
        var exitCode = await RunCommandAsync(_command,
            "--input", file1, nonExistentFile,
            "--output", outputPath,
            "--type", "Vertical");

        // Assert
        exitCode.Should().NotBe(0, "Should fail with missing input file");
    }

    [Fact]
    public async Task Merge_VerticalMerge_PreservesHeadersCorrectly()
    {
        // Arrange
        var expectedHeaders = new[] { "Product", "Price", "Stock" };
        var file1 = CreateTestCsv("inv1.csv", expectedHeaders, new[]
        {
            new[] { "Widget", "10.50", "100" }
        });

        var file2 = CreateTestCsv("inv2.csv", expectedHeaders, new[]
        {
            new[] { "Gadget", "25.00", "50" }
        });

        var outputPath = GetTempPath("merged_headers.csv");

        // Act
        var exitCode = await RunCommandAsync(_command,
            "--input", file1, file2,
            "--output", outputPath,
            "--type", "Vertical",
            "--has-header");

        // Assert
        exitCode.Should().Be(0);
        var (headers, rows) = ReadCsvWithHeaders(outputPath);
        headers.Should().BeEquivalentTo(expectedHeaders);
        rows.Count.Should().Be(2);
    }
}
