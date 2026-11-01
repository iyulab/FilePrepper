using FilePrepper.Tasks;
using FilePrepper.Tasks.RemoveColumns;
using FilePrepper.Tasks.RenameColumns;
using FilePrepper.Tasks.ReorderColumns;
using Microsoft.Extensions.Logging;
using Moq;

namespace FilePrepper.Tests.Tasks;

/// <summary>
/// remove-columns, rename-columns and reorder-columns skip a name that is not a column (one list serves files that differ), so
/// a mistyped name left its column as it was with nothing said. The names that matched nothing are now in a warning.
/// </summary>
public class AbsentColumnTests : TaskBaseTest<RemoveColumnsTask>
{
    public AbsentColumnTests(ITestOutputHelper output) : base(output)
    {
    }

    private static void VerifyWarning<T>(Mock<ILogger<T>> logger, string containing) =>
        logger.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => $"{state}".Contains(containing, StringComparison.Ordinal)),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);

    [Fact]
    public void Remove_columns_names_the_columns_it_did_not_find()
    {
        WriteTestFileLines("a,b,c", "1,2,3");
        var option = new RemoveColumnsOption { InputPath = _testInputPath, OutputPath = _testOutputPath, RemoveColumns = ["b", "bb"] };

        Assert.True(new RemoveColumnsTask(_mockLogger.Object).Execute(new TaskContext(option)));

        Assert.Equal(["a,c", "1,3"], ReadOutputFileLines());
        VerifyWarning(_mockLogger, "Not a column of the input: bb (nothing to remove)");
    }

    [Fact]
    public void Rename_columns_names_the_columns_it_did_not_find()
    {
        WriteTestFileLines("a,b", "1,2");
        var logger = new Mock<ILogger<RenameColumnsTask>>();
        var option = new RenameColumnsOption { InputPath = _testInputPath, OutputPath = _testOutputPath, RenameMap = new() { ["a"] = "x", ["bb"] = "y" } };

        Assert.True(new RenameColumnsTask(logger.Object).Execute(new TaskContext(option)));

        Assert.Equal(["x,b", "1,2"], ReadOutputFileLines());
        VerifyWarning(logger, "Not a column of the input: bb (nothing to rename)");
    }

    [Fact]
    public void Reorder_columns_names_the_columns_it_did_not_find()
    {
        WriteTestFileLines("a,b,c", "1,2,3");
        var logger = new Mock<ILogger<ReorderColumnsTask>>();
        var option = new ReorderColumnsOption { InputPath = _testInputPath, OutputPath = _testOutputPath, Order = ["c", "zz"] };

        Assert.True(new ReorderColumnsTask(logger.Object).Execute(new TaskContext(option)));

        Assert.Equal(["c,a,b", "3,1,2"], ReadOutputFileLines());
        VerifyWarning(logger, "Not a column of the input: zz (ignored)");
    }

    [Fact]
    public void A_name_that_is_a_column_is_not_reported()
    {
        WriteTestFileLines("a,b", "1,2");
        var option = new RemoveColumnsOption { InputPath = _testInputPath, OutputPath = _testOutputPath, RemoveColumns = ["a"] };

        Assert.True(new RemoveColumnsTask(_mockLogger.Object).Execute(new TaskContext(option)));

        _mockLogger.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);
    }
}
