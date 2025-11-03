using FilePrepper.Tasks;
using FilePrepper.Tasks.ReorderColumns;
using Xunit.Abstractions;

namespace FilePrepper.Tests.Tasks;

public class ReorderColumnsTests : TaskBaseTest<ReorderColumnsTask>
{
    public ReorderColumnsTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void Validate_NoOrder_ShouldReturnError()
    {
        var option = new ReorderColumnsOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Order = new List<string>()
        };
        var errors = option.Validate();

        Assert.Contains(errors, e => e.Contains("At least one column must be specified for reordering."));
    }

    [Fact]
    public void Validate_WhitespaceInOrder_ShouldReturnError()
    {
        var option = new ReorderColumnsOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Order = new List<string> { " " }
        };
        var errors = option.Validate();

        Assert.Contains(errors, e => e.Contains("cannot be empty or whitespace"));
    }

    [Fact]
    public void Execute_ReorderColumns_ShouldChangeHeaderOrder()
    {
        // CSV: A,B,C
        WriteTestFileLines(
            "A,B,C",
            "1,2,3",
            "4,5,6"
        );

        // Desired order: B,A
        var option = new ReorderColumnsOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Order = new List<string> { "B", "A" }
        };

        var task = new ReorderColumnsTask(_mockLogger.Object);
        var context = new TaskContext(option);

        bool success = task.Execute(context);
        Assert.True(success);

        var lines = ReadOutputFileLines();
        // Expect header: B,A,C (B then A then remaining C)
        Assert.Contains("B,A,C", lines[0]);
        // Data rows reordered according to header
        Assert.Contains("2,1,3", lines[1]);
        Assert.Contains("5,4,6", lines[2]);
    }

    [Fact]
    public void Execute_ReorderWithNonExistingColumns_ShouldIgnoreNonExisting()
    {
        // CSV: X,Y,Z
        WriteTestFileLines(
            "X,Y,Z",
            "a,b,c"
        );

        // Desired order includes a non-existing column "NotHere"
        var option = new ReorderColumnsOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Order = new List<string> { "NotHere", "Y" }
        };

        var task = new ReorderColumnsTask(_mockLogger.Object);
        var context = new TaskContext(option);

        bool success = task.Execute(context);
        Assert.True(success);

        var lines = ReadOutputFileLines();
        // Expected header: Y,X,Z since "NotHere" is ignored, "Y" first
        Assert.Contains("Y,X,Z", lines[0]);
    }

    [Fact]
    public void Execute_NoRecords_ShouldStillOutputHeader()
    {
        // CSV with header but no data rows
        WriteTestFileLines("A,B,C");

        var option = new ReorderColumnsOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Order = new List<string> { "C", "A" }
        };

        var task = new ReorderColumnsTask(_mockLogger.Object);
        var context = new TaskContext(option);

        bool success = task.Execute(context);
        Assert.True(success);

        var lines = ReadOutputFileLines();
        // Expect reordered header "C,A,B" (C,A then remaining B)
        Assert.Contains("C,A,B", lines[0]);
        // No data rows after header.
        Assert.Single(lines);
    }
}
