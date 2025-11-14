using FilePrepper.Tasks.Aggregate;
using FilePrepper.Tasks;
using Xunit.Abstractions;

namespace FilePrepper.Tests.Tasks;

public class AggregateTests : TaskBaseTest<AggregateTask>
{
    public AggregateTests(ITestOutputHelper output) : base(output)
    {
        // 테스트 입력 파일 생성
        File.WriteAllText(_testInputPath,
            "Region,Product,Sales\n" +
            "North,A,100\n" +
            "North,A,150\n" +
            "South,A,200\n" +
            "South,B,300\n" +
            "North,B,250\n");
    }

    [Fact]
    public void Execute_WithAppendToSource_ShouldSucceed()
    {
        // Arrange
        var options = new AggregateOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            GroupByColumns = new[] { "Region" },
            AggregateColumns = new List<AggregateColumn>
            {
                new() {
                    ColumnName = "Sales",
                    Function = AggregateFunction.Sum
                }
            },
            AppendToSource = true,
            OutputColumnTemplate = "{column}_{function}_by_{groupBy}"
        };

        var task = new AggregateTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Equal("Region,Product,Sales,Sales_Sum_by_Region", lines[0]);
        Assert.Equal(6, lines.Length);
        Assert.Contains(lines, l => l.StartsWith("North,A,100,") && l.EndsWith("500"));
    }

    [Fact]
    public void Execute_WithCustomColumnTemplate_ShouldSucceed()
    {
        // Arrange
        var options = new AggregateOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            GroupByColumns = new[] { "Region", "Product" },
            AggregateColumns = new List<AggregateColumn>
            {
                new() {
                    ColumnName = "Sales",
                    Function = AggregateFunction.Average
                }
            },
            AppendToSource = true,
            OutputColumnTemplate = "Avg_{column}_for_{groupBy}"
        };

        var task = new AggregateTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Contains("Avg_Sales_for_Region_Product", lines[0]);
    }

    [Fact]
    public void Validate_WithAppendToSourceAndNoTemplate_ShouldReturnError()
    {
        // Arrange
        var options = new AggregateOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            GroupByColumns = new[] { "Region" },
            AggregateColumns = new List<AggregateColumn>
            {
                new() {
                    ColumnName = "Sales",
                    Function = AggregateFunction.Sum
                }
            },
            AppendToSource = true,
            OutputColumnTemplate = ""  // 빈 템플릿
        };

        // Act
        string[] errors = options.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("Column template is required when appending to source"));
    }

    [Fact]
    public void Execute_WithAppendToSourceMultipleAggregations_ShouldSucceed()
    {
        // Arrange
        var options = new AggregateOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            GroupByColumns = new[] { "Region" },
            AggregateColumns = new List<AggregateColumn>
            {
                new() {
                    ColumnName = "Sales",
                    Function = AggregateFunction.Sum
                },
                new() {
                    ColumnName = "Sales",
                    Function = AggregateFunction.Average
                }
            },
            AppendToSource = true,
            OutputColumnTemplate = "{column}_{function}_by_{groupBy}"
        };

        var task = new AggregateTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Contains("Sales_Sum_by_Region", lines[0]);
        Assert.Contains("Sales_Average_by_Region", lines[0]);
        Assert.Equal(6, lines.Length);
    }

    [Fact]
    public void Execute_WithMixedNumericAndTextColumns_ShouldSucceed()
    {
        // Arrange
        var mixedDataPath = Path.GetTempFileName();
        File.WriteAllText(mixedDataPath,
            "Region,Category,Sales,Rating\n" +
            "North,A,100,4.5\n" +
            "North,A,150,4.2\n" +
            "South,B,200,3.8\n");

        var options = new AggregateOption
        {
            InputPath = mixedDataPath,
            OutputPath = _testOutputPath,
            GroupByColumns = new[] { "Region", "Category" },
            AggregateColumns = new List<AggregateColumn>
            {
                new() {
                    ColumnName = "Sales",
                    Function = AggregateFunction.Sum
                },
                new() {
                    ColumnName = "Rating",
                    Function = AggregateFunction.Average
                }
            },
            AppendToSource = true,
            OutputColumnTemplate = "{column}_{function}_by_{groupBy}"
        };

        var task = new AggregateTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Contains("Sales_Sum_by_Region_Category", lines[0]);
        Assert.Contains("Rating_Average_by_Region_Category", lines[0]);

        // Cleanup
        File.Delete(mixedDataPath);
    }

    [Fact]
    public void Execute_WithEmptyGroup_ShouldHandleGracefully()
    {
        // Arrange
        var emptyGroupPath = Path.GetTempFileName();
        File.WriteAllText(emptyGroupPath,
            "Region,Sales\n" +
            "North,100\n" +
            ",150\n" +  // 빈 Region
            "South,200\n");

        var options = new AggregateOption
        {
            InputPath = emptyGroupPath,
            OutputPath = _testOutputPath,
            GroupByColumns = new[] { "Region" },
            AggregateColumns = new List<AggregateColumn>
            {
                new() {
                    ColumnName = "Sales",
                    Function = AggregateFunction.Sum
                }
            },
            AppendToSource = true,
            OutputColumnTemplate = "{column}_{function}_by_{groupBy}"
        };

        var task = new AggregateTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Equal(4, lines.Length);
        Assert.Contains(lines, l => l.StartsWith(",150,"));

        // Cleanup
        File.Delete(emptyGroupPath);
    }
}