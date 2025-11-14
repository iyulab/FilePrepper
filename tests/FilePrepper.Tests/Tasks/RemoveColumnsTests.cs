using FilePrepper.Tasks;
using FilePrepper.Tasks.RemoveColumns;
using Xunit.Abstractions;

namespace FilePrepper.Tests.Tasks;

public class RemoveColumnsTests : TaskBaseTest<RemoveColumnsTask>
{
    public RemoveColumnsTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void Validate_NoColumns_ShouldReturnError()
    {
        // Arrange
        var option = new RemoveColumnsOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            RemoveColumns = new List<string>() // empty
        };

        // Act
        var errors = option.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("At least one column must be specified"));
    }

    [Fact]
    public void Validate_WhitespaceColumnName_ShouldReturnError()
    {
        // Arrange
        var option = new RemoveColumnsOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            RemoveColumns = new List<string> { " " } // whitespace
        };

        // Act
        var errors = option.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("cannot be empty or whitespace"));
    }

    [Fact]
    public void Execute_RemoveSingleColumn_ShouldSucceed()
    {
        // Arrange
        // CSV with three columns: "Id", "Name", "Extra".
        // We'll remove "Extra".
        WriteTestFileLines(
            "Id,Name,Extra",
            "1,Alice,Something",
            "2,Bob,Anything"
        );

        var option = new RemoveColumnsOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            RemoveColumns = new List<string> { "Extra" }
        };

        var task = new RemoveColumnsTask(_mockLogger.Object);
        var context = new TaskContext(option);

        // Act
        bool success = task.Execute(context);

        // Assert
        Assert.True(success);

        var lines = ReadOutputFileLines();
        // We expect two columns now: "Id" and "Name"
        Assert.Equal(3, lines.Length); // header + 2 rows
        Assert.Contains("Id,Name", lines[0]);
        Assert.Contains("1,Alice", lines[1]);
        Assert.Contains("2,Bob", lines[2]);
        // Make sure "Extra" is gone
        Assert.DoesNotContain("Extra", lines[0]);
    }

    [Fact]
    public void Execute_RemoveMultipleColumns_OneNotPresent_ShouldStillSucceed()
    {
        // Arrange
        // "Extra2" doesn't exist
        WriteTestFileLines(
            "Id,Name,Extra",
            "1,Alice,Something",
            "2,Bob,Anything"
        );

        var option = new RemoveColumnsOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            RemoveColumns = new List<string> { "Extra", "Extra2" }
        };

        var task = new RemoveColumnsTask(_mockLogger.Object);
        var context = new TaskContext(option);

        // Act
        bool success = task.Execute(context);

        // Assert
        Assert.True(success);

        var lines = ReadOutputFileLines();
        // "Extra" was removed, "Extra2" wasn't present => no error
        Assert.Contains("Id,Name", lines[0]);
        Assert.DoesNotContain("Extra", lines[0]);
    }

    [Fact]
    public void Execute_NoRecords_ShouldSucceedAndProduceHeaderOnly()
    {
        // Arrange
        // CSV with only header, no data rows
        WriteTestFileLines(
            "Id,Name,Extra"
        );

        var option = new RemoveColumnsOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            RemoveColumns = new List<string> { "Extra" }
        };

        var task = new RemoveColumnsTask(_mockLogger.Object);
        var context = new TaskContext(option);

        // Act
        bool success = task.Execute(context);

        // Assert
        Assert.True(success);
        var lines = ReadOutputFileLines();
        // We expect just 1 line (the header), with "Extra" removed
        Assert.Single(lines);
        Assert.Contains("Id,Name", lines[0]);
        Assert.DoesNotContain("Extra", lines[0]);
    }

    [Fact]
    public void Execute_ShouldIgnoreMissingColumnsForAllRows()
    {
        // Arrange
        // 2 rows, "Extra" missing in second row
        WriteTestFileLines(
            "Id,Name,Extra",
            "1,Alice,Something",
            "2,Bob,"
        );

        var option = new RemoveColumnsOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            RemoveColumns = new List<string> { "Extra" }
        };

        var task = new RemoveColumnsTask(_mockLogger.Object);
        var context = new TaskContext(option);

        // Act
        bool success = task.Execute(context);

        // Assert
        Assert.True(success);
        var lines = ReadOutputFileLines();
        // Both rows should no longer contain "Extra"
        Assert.Equal(3, lines.Length);
        Assert.Contains("Id,Name", lines[0]);
        Assert.Contains("1,Alice", lines[1]);
        Assert.Contains("2,Bob", lines[2]);
    }
}
