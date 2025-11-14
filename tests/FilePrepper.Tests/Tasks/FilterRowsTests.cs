using Xunit;
using FilePrepper.Tasks.FilterRows;
using Moq;
using Xunit.Abstractions;
using FilePrepper.Tasks;

namespace FilePrepper.Tests.Tasks;

public class FilterRowsTests : TaskBaseTest<FilterRowsTask>
{
    public FilterRowsTests(ITestOutputHelper output) : base(output)
    {
        WriteTestFileLines(
            "Id,Name,Age,Score",
            "1,John,20,85",
            "2,Jane,22,90",
            "3,Mary,25,90",
            "4,Tom,30,70",
            "5,Chris,40,90",
            "6,Alice,25,70"
        );
    }

    [Fact]
    public void Execute_EqualsOperator_ShouldFilterCorrectly()
    {
        // Arrange
        var options = new FilterRowsOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Conditions = new List<FilterCondition>
            {
                new()
                {
                    ColumnName = "Name",
                    Operator = FilterOperator.Equals,
                    Value = "Tom"
                }
            }
        };

        var task = new FilterRowsTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        var lines = ReadOutputFileLines();
        // 결과는 헤더 + Tom 한 줄
        Assert.Equal(2, lines.Length);
        Assert.Contains("Tom", lines[1]);
    }

    [Fact]
    public void Execute_NotEqualsOperator_ShouldFilterCorrectly()
    {
        // Arrange
        var options = new FilterRowsOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Conditions = new List<FilterCondition>
            {
                new()
                {
                    ColumnName = "Name",
                    Operator = FilterOperator.NotEquals,
                    Value = "Tom"
                }
            }
        };

        var task = new FilterRowsTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        var lines = ReadOutputFileLines();
        // Tom을 제외한 모든 행(5행) + 헤더 = 6줄
        Assert.Equal(6, lines.Length);
        Assert.DoesNotContain("Tom", lines[5]);
    }

    [Fact]
    public void Execute_ContainsOperator_ShouldFilterCorrectly()
    {
        // Arrange
        var options = new FilterRowsOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Conditions = new List<FilterCondition>
        {
            new()
            {
                ColumnName = "Name",
                Operator = FilterOperator.Contains,
                Value = "a"
            }
        }
        };

        var task = new FilterRowsTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        var lines = ReadOutputFileLines();
        // "Jane", "Mary", "Alice" => 3행 + 헤더 1행 = 총 4행
        Assert.Equal(4, lines.Length);
        Assert.Contains("Jane", lines[1]);
        Assert.Contains("Mary", lines[2]);
        Assert.Contains("Alice", lines[3]);
    }

    [Fact]
    public void Execute_GreaterThanOperator_ShouldFilterCorrectly()
    {
        // Arrange
        var options = new FilterRowsOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Conditions = new List<FilterCondition>
            {
                new()
                {
                    ColumnName = "Score",
                    Operator = FilterOperator.GreaterThan,
                    Value = "80"
                }
            }
        };

        var task = new FilterRowsTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        var lines = ReadOutputFileLines();
        // 85, 90, 90, 90 => 4행 + 헤더 = 5줄
        Assert.Equal(5, lines.Length);
    }

    [Fact]
    public void Validate_NoConditions_ShouldReturnError()
    {
        // Arrange
        var options = new FilterRowsOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Conditions = new List<FilterCondition>()
        };

        // Act
        string[] errors = options.Validate();

        // Assert
        Assert.Contains("Filter conditions must not be empty.", errors);
    }
}
