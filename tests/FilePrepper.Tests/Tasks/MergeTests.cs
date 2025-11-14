using FilePrepper.Tasks;
using FilePrepper.Tasks.Merge;
using Xunit.Abstractions;

namespace FilePrepper.Tests.Tasks;

public class MergeTests : TaskBaseTest<MergeTask>
{
    public MergeTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task HorizontalMerge_WithoutKeys_DifferentRowCount_ShouldThrowError()
    {
        // Arrange
        WriteTestFileLines(
            "Name,Age",
            "John,25",
            "Jane,30"
        );
        var secondInputPath = Path.GetTempFileName();
        File.WriteAllLines(secondInputPath, new[]
        {
            "City,Country",
            "Seoul,Korea"  // Only one row
        });

        var options = new MergeOption
        {
            OutputPath = _testOutputPath,
            InputPaths = new List<string> { _testInputPath, secondInputPath },
            MergeType = MergeType.Horizontal,
            HasHeader = true,
            IgnoreErrors = false
        };

        var context = CreateContext(options);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(() => ExecuteTaskAsync(context));
        Assert.Contains("Row count mismatch", exception.Message);
        File.Delete(secondInputPath);
    }

    [Fact]
    public async Task VerticalMerge_DifferentColumnCount_ShouldThrowError()
    {
        // Arrange
        WriteTestFileLines(
            "Col1,Col2",
            "John,25",
            "Jane,30"
        );
        var secondInputPath = Path.GetTempFileName();
        File.WriteAllLines(secondInputPath, new[]
        {
        "Col3,Col4,Col5",  // Different columns, no overlap
        "Mike,35,Seoul",
        "Sara,28,Tokyo"
    });
        var options = new MergeOption
        {
            OutputPath = _testOutputPath,
            InputPaths = new List<string> { _testInputPath, secondInputPath },
            MergeType = MergeType.Vertical,
            HasHeader = true,
            IgnoreErrors = false
        };

        var task = new MergeTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(() => task.ExecuteAsync(context));
        Assert.Contains("Column count mismatch", exception.Message);
        File.Delete(secondInputPath);
    }

    [Fact]
    public async Task VerticalMerge_WithHeaders_ShouldMergeCorrectly()
    {
        // Arrange
        WriteTestFileLines(
            "Name,Age",
            "John,25",
            "Jane,30"
        );

        var secondInputPath = Path.GetTempFileName();
        File.WriteAllLines(secondInputPath, new[]
        {
            "Name,Age",
            "Mike,35",
            "Sara,28"
        });

        var options = new MergeOption
        {
            OutputPath = _testOutputPath,
            InputPaths = new List<string> { _testInputPath, secondInputPath },
            MergeType = MergeType.Vertical,
            HasHeader = true,
            IgnoreErrors = false
        };

        var context = CreateContext(options);

        // Act
        var result = await ExecuteTaskAsync(context);

        // Assert
        Assert.True(result);
        var outputLines = ReadOutputFileLines();
        Assert.Equal(5, outputLines.Length); // Header + 4 data rows
        Assert.Equal("Name,Age", outputLines[0]);
        Assert.Contains("John,25", outputLines);
        Assert.Contains("Jane,30", outputLines);
        Assert.Contains("Mike,35", outputLines);
        Assert.Contains("Sara,28", outputLines);

        File.Delete(secondInputPath);
    }

    private TaskContext CreateContext(MergeOption options)
    {
        // MergeTask는 첫 번째 InputPath를 기본 입력으로 사용
        return new TaskContext(options);
    }

    private async Task<bool> ExecuteTaskAsync(TaskContext context)
    {
        var task = new MergeTask(_mockLogger.Object);
        return await task.ExecuteAsync(context);
    }

    [Fact]
    public async Task HorizontalMerge_WithoutKeys_ShouldMergeCorrectly()
    {
        // Arrange
        WriteTestFileLines(
            "Name,Age",
            "John,25",
            "Jane,30"
        );

        var secondInputPath = Path.GetTempFileName();
        File.WriteAllLines(secondInputPath, new[]
        {
            "City,Country",
            "Seoul,Korea",
            "Tokyo,Japan"
        });

        var options = new MergeOption
        {
            OutputPath = _testOutputPath,
            InputPaths = new List<string> { _testInputPath, secondInputPath },
            MergeType = MergeType.Horizontal,
            HasHeader = true
        };

        var task = new MergeTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        var result = await task.ExecuteAsync(context);

        // Assert
        Assert.True(result);
        var outputLines = ReadOutputFileLines();
        Assert.Equal(3, outputLines.Length); // Header + 2 data rows
        Assert.Equal("Name,Age,City,Country", outputLines[0]);
        Assert.Contains("John,25,Seoul,Korea", outputLines);
        Assert.Contains("Jane,30,Tokyo,Japan", outputLines);

        File.Delete(secondInputPath);
    }

    [Fact]
    public async Task HorizontalMerge_WithoutKeys_DuplicateColumns_ShouldHandleCorrectly()
    {
        // Arrange
        WriteTestFileLines(
            "Name,Age",
            "John,25",
            "Jane,30"
        );

        var secondInputPath = Path.GetTempFileName();
        File.WriteAllLines(secondInputPath, new[]
        {
            "Name,Country",  // Name is duplicate
            "Mike,Korea",
            "Sara,Japan"
        });

        var options = new MergeOption
        {
            OutputPath = _testOutputPath,
            InputPaths = new List<string> { _testInputPath, secondInputPath },
            MergeType = MergeType.Horizontal,
            HasHeader = true
        };

        var task = new MergeTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        var result = await task.ExecuteAsync(context);

        // Assert
        Assert.True(result);
        var outputLines = ReadOutputFileLines();
        Assert.Equal(3, outputLines.Length);
        Assert.Equal("Name,Age,Name_2,Country", outputLines[0]);
        Assert.Contains("John,25,Mike,Korea", outputLines);
        Assert.Contains("Jane,30,Sara,Japan", outputLines);

        File.Delete(secondInputPath);
    }

    [Fact]
    public async Task HorizontalMerge_WithKeys_ShouldJoinCorrectly()
    {
        // Arrange
        WriteTestFileLines(
            "ID,Name,Age",
            "1,John,25",
            "2,Jane,30",
            "3,Mike,35"
        );

        var secondInputPath = Path.GetTempFileName();
        File.WriteAllLines(secondInputPath, new[]
        {
            "ID,City,Country",
            "1,Seoul,Korea",
            "2,Tokyo,Japan",
            "4,London,UK"
        });

        var options = new MergeOption
        {
            OutputPath = _testOutputPath,
            InputPaths = new List<string> { _testInputPath, secondInputPath },
            MergeType = MergeType.Horizontal,
            JoinType = JoinType.Inner,
            JoinKeyColumns = new List<ColumnIdentifier> { ColumnIdentifier.ByName("ID") },
            HasHeader = true
        };

        var task = new MergeTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        var result = await task.ExecuteAsync(context);

        // Assert
        Assert.True(result);
        var outputLines = ReadOutputFileLines();
        Assert.Equal(3, outputLines.Length); // Header + 2 matching records
        Assert.Equal("ID,Name,Age,City,Country", outputLines[0]);
        Assert.Contains("1,John,25,Seoul,Korea", outputLines);
        Assert.Contains("2,Jane,30,Tokyo,Japan", outputLines);

        File.Delete(secondInputPath);
    }

    [Fact]
    public async Task VerticalMerge_SimpleNumericData_ShouldMergeCorrectly()
    {
        // Arrange
        WriteTestFileLines(
            "1,1,1,1,1",
            "2,2,2,2,2",
            "3,3,3,3,3"
        );

        var secondInputPath = Path.GetTempFileName();
        File.WriteAllLines(secondInputPath, new[]
        {
        "4,4,4,4,4",
        "5,5,5,5,5"
    });

        var options = new MergeOption
        {
            OutputPath = _testOutputPath,
            InputPaths = new List<string> { _testInputPath, secondInputPath },
            MergeType = MergeType.Vertical,
            HasHeader = false
        };

        var task = new MergeTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        var result = await task.ExecuteAsync(context);

        // Assert
        Assert.True(result);
        var outputLines = ReadOutputFileLines();
        Assert.Equal(5, outputLines.Length); // All rows combined
        Assert.Equal("1,1,1,1,1", outputLines[0]);
        Assert.Equal("2,2,2,2,2", outputLines[1]);
        Assert.Equal("3,3,3,3,3", outputLines[2]);
        Assert.Equal("4,4,4,4,4", outputLines[3]);
        Assert.Equal("5,5,5,5,5", outputLines[4]);

        File.Delete(secondInputPath);
    }

    [Fact]
    public async Task HorizontalMerge_SimpleData_ShouldMergeCorrectly()
    {
        // Arrange
        WriteTestFileLines(
            "1,1,1",
            "2,2,2"
        );

        var secondInputPath = Path.GetTempFileName();
        File.WriteAllLines(secondInputPath, new[]
        {
        "r1,r1",
        "r2,r2"
    });

        var options = new MergeOption
        {
            OutputPath = _testOutputPath,
            InputPaths = new List<string> { _testInputPath, secondInputPath },
            MergeType = MergeType.Horizontal,
            HasHeader = false
        };

        var task = new MergeTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        var result = await task.ExecuteAsync(context);

        // Assert
        Assert.True(result);
        var outputLines = ReadOutputFileLines();
        Assert.Equal(2, outputLines.Length);
        Assert.Equal("1,1,1,r1,r1", outputLines[0]);
        Assert.Equal("2,2,2,r2,r2", outputLines[1]);

        File.Delete(secondInputPath);
    }

}