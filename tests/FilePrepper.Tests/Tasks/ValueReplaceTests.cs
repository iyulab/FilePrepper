using FilePrepper.Tasks.ValueReplace;
using FilePrepper.Tasks;
using Xunit.Abstractions;

namespace FilePrepper.Tests.Tasks;

public class ValueReplaceTests : TaskBaseTest<ValueReplaceTask>
{
    public ValueReplaceTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void Validate_NoReplaceMethods_ShouldReturnError()
    {
        // Arrange
        var option = new ValueReplaceOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Name" },
            ReplaceMethods = new List<ColumnReplaceMethod>()
        };

        // Act
        var errors = option.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("At least one replace method must be specified."));
    }

    [Fact]
    public void Validate_EmptyColumnName_ShouldReturnError()
    {
        // Arrange
        var option = new ValueReplaceOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Name" },
            ReplaceMethods = new List<ColumnReplaceMethod>
            {
                new ColumnReplaceMethod
                {
                    ColumnName = "",
                    Replacements = new Dictionary<string, string> { { "old", "new" } }
                }
            }
        };

        // Act
        var errors = option.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("Column name cannot be empty or whitespace."));
    }

    [Fact]
    public void Validate_NoReplacements_ShouldReturnError()
    {
        // Arrange
        var option = new ValueReplaceOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Name" },
            ReplaceMethods = new List<ColumnReplaceMethod>
            {
                new ColumnReplaceMethod
                {
                    ColumnName = "Name",
                    Replacements = new Dictionary<string, string>()
                }
            }
        };

        // Act
        var errors = option.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("Replacements must be specified for column Name."));
    }

    [Fact]
    public void Execute_ValueReplace_ShouldSucceed()
    {
        // Arrange
        WriteTestFileLines(
            "Id,Name,Status",
            "1,John,Active",
            "2,Jane,Inactive",
            "3,Mary,Active",
            "4,Tom,Pending"
        );

        var options = new ValueReplaceOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Name", "Status" },
            ReplaceMethods = new List<ColumnReplaceMethod>
            {
                new ColumnReplaceMethod
                {
                    ColumnName = "Status",
                    Replacements = new Dictionary<string, string>
                    {
                        { "Active", "1" },
                        { "Inactive", "0" },
                        { "Pending", "-1" }
                    }
                }
            }
        };

        var task = new ValueReplaceTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        var lines = ReadOutputFileLines();

        // 헤더 + 4행 = 5라인
        Assert.Equal(5, lines.Length);
        // Status 컬럼 값이 대체되었는지 확인
        Assert.Contains("1,John,1", lines[1]);
        Assert.Contains("2,Jane,0", lines[2]);
        Assert.Contains("3,Mary,1", lines[3]);
        Assert.Contains("4,Tom,-1", lines[4]);
    }
}
