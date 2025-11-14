using FilePrepper.Tasks;
using FilePrepper.Tasks.RenameColumns;
using Xunit.Abstractions;

namespace FilePrepper.Tests.Tasks;

public class RenameColumnsTests : TaskBaseTest<RenameColumnsTask>
{
    public RenameColumnsTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void Validate_NoMappings_ShouldReturnError()
    {
        // Arrange
        var option = new RenameColumnsOption 
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            RenameMap = new Dictionary<string, string>() 
        };

        // Act
        var errors = option.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("At least one column rename mapping must be specified."));
    }

    [Fact]
    public void Validate_WhitespaceKeyOrValue_ShouldReturnError()
    {
        // Arrange
        var option = new RenameColumnsOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            RenameMap = new Dictionary<string, string>
            {
                { "OldCol", " " },
                { " ", "NewCol" }
            }
        };

        // Act
        var errors = option.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("New column name cannot be empty or whitespace."));
        Assert.Contains(errors, e => e.Contains("Original column name cannot be empty or whitespace."));
    }

    [Fact]
    public void Execute_RenameColumn_ShouldSucceed()
    {
        // Arrange
        WriteTestFileLines(
            "Id,OldName,Score",
            "1,Alice,100",
            "2,Bob,200"
        );

        var option = new RenameColumnsOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            RenameMap = new Dictionary<string, string>
            {
                { "OldName", "NewName" }
            }
        };

        var task = new RenameColumnsTask(_mockLogger.Object);
        var context = new TaskContext(option);

        // Act
        bool success = task.Execute(context);
        Assert.True(success);

        var lines = ReadOutputFileLines();
        // Expect header: Id,NewName,Score
        Assert.Contains("Id,NewName,Score", lines[0]);
        Assert.Contains("1,Alice,100", lines[1]);
        Assert.Contains("2,Bob,200", lines[2]);
    }

    [Fact]
    public void Execute_NoMatchingColumn_ShouldLeaveDataUnchanged()
    {
        // Arrange
        WriteTestFileLines(
            "Id,Name,Score",
            "1,Alice,100",
            "2,Bob,200"
        );

        var option = new RenameColumnsOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            RenameMap = new Dictionary<string, string>
            {
                { "NonExist", "X" }
            }
        };

        var task = new RenameColumnsTask(_mockLogger.Object);
        var context = new TaskContext(option);

        // Act
        bool success = task.Execute(context);
        Assert.True(success);

        var lines = ReadOutputFileLines();
        // Header and rows unchanged
        Assert.Contains("Id,Name,Score", lines[0]);
        Assert.Contains("1,Alice,100", lines[1]);
        Assert.Contains("2,Bob,200", lines[2]);
    }

    [Fact]
    public void Execute_MultipleRenames_ShouldSucceed()
    {
        // Arrange
        WriteTestFileLines(
            "A,B,C",
            "1,2,3",
            "4,5,6"
        );

        var option = new RenameColumnsOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            RenameMap = new Dictionary<string, string>
            {
                { "A", "X" },
                { "B", "Y" },
                { "NonExist", "Z" } // should be skipped
            }
        };

        var task = new RenameColumnsTask(_mockLogger.Object);
        var context = new TaskContext(option);

        // Act
        bool success = task.Execute(context);
        Assert.True(success);

        var lines = ReadOutputFileLines();
        // Expected header: X,Y,C (since A->X, B->Y, C unchanged)
        Assert.Contains("X,Y,C", lines[0]);
        Assert.Contains("1,2,3", lines[1]);
        Assert.Contains("4,5,6", lines[2]);
    }
}
