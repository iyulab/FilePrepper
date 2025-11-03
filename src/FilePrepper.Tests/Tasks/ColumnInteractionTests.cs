using FilePrepper.Tasks.ColumnInteraction;
using FilePrepper.Tasks;
using Xunit.Abstractions;
using System.Globalization;

namespace FilePrepper.Tests.Tasks;

public class ColumnInteractionTests : TaskBaseTest<ColumnInteractionTask>
{
    public ColumnInteractionTests(ITestOutputHelper output) : base(output)
    {
        // 테스트 입력 파일 생성
        File.WriteAllText(_testInputPath,
            "Value1,Value2,Value3,Text1,Text2\n" +
            "10,20,30,Hello,World\n" +
            "15,25,35,Good,Morning\n" +
            "5,15,25,Test,Data\n");
    }

    [Theory]
    [InlineData(OperationType.Add, new[] { "Value1", "Value2" }, "Sum", "30,40,20")]
    [InlineData(OperationType.Multiply, new[] { "Value1", "Value2" }, "Product", "200,375,75")]
    [InlineData(OperationType.Subtract, new[] { "Value1", "Value2" }, "Difference", "-10,-10,-10")]
    [InlineData(OperationType.Divide, new[] { "Value1", "Value2" }, "Quotient", "0.5,0.6,0.3333")]
    public void Execute_WithNumericOperations_ShouldSucceed(
        OperationType operation,
        string[] sourceColumns,
        string outputColumn,
        string expectedResults)
    {
        // Arrange
        var options = new ColumnInteractionOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            SourceColumns = sourceColumns,
            Operation = operation,
            OutputColumn = outputColumn
        };

        var task = new ColumnInteractionTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Contains(outputColumn, lines[0]); // 헤더 확인

        var actualResults = lines.Skip(1)
            .Select(l => l.Split(',').Last())
            .ToArray();

        var expected = expectedResults.Split(',');
        for (int i = 0; i < expected.Length; i++)
        {
            var expectedValue = double.Parse(expected[i], CultureInfo.InvariantCulture);
            var actualValue = double.Parse(actualResults[i], CultureInfo.InvariantCulture);
            Assert.True(Math.Abs(expectedValue - actualValue) < 0.001);
        }
    }

    [Fact]
    public void Execute_WithDuplicateOutputColumn_ShouldFail()
    {
        // Arrange
        var options = new ColumnInteractionOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            SourceColumns = new[] { "Value1", "Value2" },
            Operation = OperationType.Add,
            OutputColumn = "Value1",  // 이미 존재하는 컬럼명
            IgnoreErrors = false
        };

        var task = new ColumnInteractionTask(_mockLogger.Object);
        var context = new TaskContext(options);
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => task.Execute(context));
        Assert.Equal("Output column already exists: Value1", exception.Message);
    }

    [Fact]
    public void Execute_WithInvalidData_AndNoIgnoreErrors_ShouldFail()
    {
        // 잘못된 데이터가 포함된 파일 생성
        var invalidDataPath = Path.GetTempFileName();
        File.WriteAllText(invalidDataPath,
            "Value1,Value2\n" +
            "10,20\n" +
            "invalid,25\n");

        var options = new ColumnInteractionOption
        {
            InputPath = invalidDataPath,
            OutputPath = _testOutputPath,
            SourceColumns = new[] { "Value1", "Value2" },
            Operation = OperationType.Add,
            OutputColumn = "Sum",
            IgnoreErrors = false
        };

        var task = new ColumnInteractionTask(_mockLogger.Object);
        var context = new TaskContext(options);
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => task.Execute(context));
        Assert.Contains("Invalid numeric value", exception.Message);

        // Cleanup
        File.Delete(invalidDataPath);
    }

    [Fact]
    public void Execute_WithConcat_ShouldSucceed()
    {
        // Arrange
        var options = new ColumnInteractionOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            SourceColumns = new[] { "Text1", "Text2" },
            Operation = OperationType.Concat,
            OutputColumn = "CombinedText"
        };

        var task = new ColumnInteractionTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Contains("HelloWorld", lines[1]);
        Assert.Contains("GoodMorning", lines[2]);
        Assert.Contains("TestData", lines[3]);
    }

    [Fact]
    public void Execute_WithCustomExpression_ShouldSucceed()
    {
        // Arrange
        var options = new ColumnInteractionOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            SourceColumns = new[] { "Value1", "Value2", "Value3" },
            Operation = OperationType.Custom,
            OutputColumn = "CustomCalc",
            CustomExpression = "$1 + $2 * $3"  // ($1 = Value1, $2 = Value2, $3 = Value3)
        };

        var task = new ColumnInteractionTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Contains("CustomCalc", lines[0]);

        // 계산 결과 확인: Value1 + Value2 * Value3
        var firstRow = lines[1].Split(',');
        var expectedValue = 10 + 20 * 30;  // 610
        var actualValue = double.Parse(firstRow[^1], CultureInfo.InvariantCulture);
        Assert.Equal(expectedValue, actualValue);
    }

    [Fact]
    public void Execute_WithInvalidData_AndIgnoreErrors_ShouldSucceed()
    {
        // 잘못된 데이터가 포함된 파일 생성
        var invalidDataPath = Path.GetTempFileName();
        File.WriteAllText(invalidDataPath,
            "Value1,Value2\n" +
            "10,20\n" +
            "invalid,25\n" +
            "15,error\n");

        var options = new ColumnInteractionOption
        {
            InputPath = invalidDataPath,
            OutputPath = _testOutputPath,
            SourceColumns = new[] { "Value1", "Value2" },
            Operation = OperationType.Add,
            OutputColumn = "Sum",
            IgnoreErrors = true,
            DefaultValue = "0"
        };

        var task = new ColumnInteractionTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Equal(4, lines.Length); // 헤더 + 3개 데이터 행

        // Cleanup
        File.Delete(invalidDataPath);
    }

    [Fact]
    public void Execute_WithDivisionByZero_AndIgnoreErrors_ShouldUseDefaultValue()
    {
        // 0으로 나누기가 포함된 파일 생성
        var divisionByZeroPath = Path.GetTempFileName();
        File.WriteAllText(divisionByZeroPath,
            "Value1,Value2\n" +
            "10,2\n" +
            "15,0\n" +  // 0으로 나누기 시도
            "20,4\n");

        var options = new ColumnInteractionOption
        {
            InputPath = divisionByZeroPath,
            OutputPath = _testOutputPath,
            SourceColumns = new[] { "Value1", "Value2" },
            Operation = OperationType.Divide,
            OutputColumn = "Division",
            IgnoreErrors = true,
            DefaultValue = "-1"
        };

        var task = new ColumnInteractionTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        var values = lines.Skip(1).Select(l => l.Split(',').Last()).ToArray();
        Assert.Equal("5", values[0]);  // 10/2 = 5
        Assert.Equal("-1", values[1]); // 15/0 -> default value
        Assert.Equal("5", values[2]);  // 20/4 = 5

        // Cleanup
        File.Delete(divisionByZeroPath);
    }

    [Fact]
    public void Validate_WithNullSourceColumns_ShouldReturnError()
    {
        // Arrange
        var options = new ColumnInteractionOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            SourceColumns = null!,
            Operation = OperationType.Add,
            OutputColumn = "Result"
        };

        // Act
        string[] errors = options.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("At least two source columns must be specified"));
    }

    [Fact]
    public void Validate_WithEmptySourceColumns_ShouldReturnError()
    {
        // Arrange
        var options = new ColumnInteractionOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            SourceColumns = Array.Empty<string>(),
            Operation = OperationType.Add,
            OutputColumn = "Result"
        };

        // Act
        string[] errors = options.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("At least two source columns must be specified"));
    }

    [Fact]
    public void Validate_WithCustomOperationAndNoExpression_ShouldReturnError()
    {
        // Arrange
        var options = new ColumnInteractionOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            SourceColumns = new[] { "Value1", "Value2" },
            Operation = OperationType.Custom,
            OutputColumn = "Result",
            CustomExpression = null
        };

        // Act
        string[] errors = options.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("Custom expression cannot be empty when using Custom operation type"));
    }

    [Fact]
    public void Validate_WithInvalidDefaultValue_ShouldReturnError()
    {
        // Arrange
        var options = new ColumnInteractionOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            SourceColumns = new[] { "Value1", "Value2" },
            Operation = OperationType.Add,
            OutputColumn = "Result",
            DefaultValue = "not-a-number"
        };

        // Act
        string[] errors = options.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("Default value must be a valid number for numeric operations"));
    }

    [Fact]
    public void Execute_WithThreeSourceColumns_ShouldSucceed()
    {
        // Arrange
        var options = new ColumnInteractionOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            SourceColumns = new[] { "Value1", "Value2", "Value3" },
            Operation = OperationType.Add,
            OutputColumn = "Total"
        };

        var task = new ColumnInteractionTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Contains("Total", lines[0]);

        var firstRowSum = lines[1].Split(',').Last();
        Assert.Equal("60", firstRowSum); // 10 + 20 + 30
    }

    [Fact]
    public void Execute_WithComplexCustomExpression_ShouldSucceed()
    {
        // Arrange
        var options = new ColumnInteractionOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            SourceColumns = new[] { "Value1", "Value2", "Value3" },
            Operation = OperationType.Custom,
            OutputColumn = "ComplexCalc",
            CustomExpression = "($1 + $2) * $3 / 2"
        };

        var task = new ColumnInteractionTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        var firstRowResult = double.Parse(lines[1].Split(',').Last(), CultureInfo.InvariantCulture);
        var expectedValue = (10.0 + 20.0) * 30.0 / 2.0;
        Assert.Equal(expectedValue, firstRowResult);
    }
}