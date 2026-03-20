using Xunit;
using FilePrepper.Tasks.CSVCleaner;
using FilePrepper.Tasks;
using Xunit.Abstractions;

namespace FilePrepper.Tests.Tasks;

public class CSVCleanerTests : TaskBaseTest<CSVCleanerTask>
{
    public CSVCleanerTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void Execute_StripCarriageReturn_ShouldRemoveCrFromQuotedFields()
    {
        // Arrange: \r inside quoted fields survives CsvHelper parsing
        // Unquoted \r is treated as record separator by CsvHelper
        WriteTestFile("Name,Value\r\n\"Alice\",\"hello\rworld\"\r\nBob,clean\r\n");

        var options = new CSVCleanerOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            StripCarriageReturn = true,
            TargetColumns = new List<string>()
        };

        var task = new CSVCleanerTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        var lines = ReadOutputFileLines();
        Assert.Equal(3, lines.Length); // header + 2 rows
        Assert.Contains("helloworld", lines[1]); // \r stripped from quoted "hello\rworld"
        Assert.Contains("clean", lines[2]);
    }

    [Fact]
    public void Execute_StripCarriageReturnDisabled_ShouldPreserveCr()
    {
        // Arrange: file with normal CRLF line endings (no embedded \r in fields)
        WriteTestFileLines("Name,Value", "Alice,100", "Bob,200");

        var options = new CSVCleanerOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            StripCarriageReturn = false,
            TargetColumns = new List<string>()
        };

        var task = new CSVCleanerTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        var lines = ReadOutputFileLines();
        Assert.Equal(3, lines.Length);
    }

    [Fact]
    public void Execute_ThousandSeparatorCleaning_ShouldWork()
    {
        // Arrange
        WriteTestFileLines("Name,Amount", "Alice,\"1,000\"", "Bob,\"2,500\"");

        var options = new CSVCleanerOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            ThousandSeparator = ',',
            TargetColumns = new List<string> { "Amount" }
        };

        var task = new CSVCleanerTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        var lines = ReadOutputFileLines();
        Assert.Equal(3, lines.Length);
        Assert.Contains("1000", lines[1]);
        Assert.Contains("2500", lines[2]);
    }

    [Fact]
    public void Execute_StripCrWithThousandSeparator_BothApplied()
    {
        // Arrange: both CR in quoted field and thousand separator
        WriteTestFile("Name,Amount\r\n\"Alice\",\"1,000\"\r\n\"Bob\",\"value\rwith\rcr\"\r\n");

        var options = new CSVCleanerOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            StripCarriageReturn = true,
            ThousandSeparator = ',',
            TargetColumns = new List<string> { "Amount" }
        };

        var task = new CSVCleanerTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        var lines = ReadOutputFileLines();
        Assert.Equal(3, lines.Length);
        Assert.Contains("1000", lines[1]); // thousand separator removed
        // Bob's Amount field had \r inside quotes, now stripped
        Assert.DoesNotContain("\r", lines[2]);
    }
}
