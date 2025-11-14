using FilePrepper.Tasks.DataTypeConvert;
using FilePrepper.Tasks;
using Xunit.Abstractions;
using System.Globalization;

namespace FilePrepper.Tests.Tasks;

public class DataTypeConvertTests : TaskBaseTest<DataTypeConvertTask>
{
    public DataTypeConvertTests(ITestOutputHelper output) : base(output)
    {
        // 테스트 입력 파일 생성
        File.WriteAllText(_testInputPath,
            "IntValue,DecimalValue,DateValue,BoolValue,StringValue\n" +
            "123,123.45,2024-01-10,true,Hello\n" +
            "456,456.78,2024-02-20,false,World\n" +
            "789,789.12,2024-03-30,yes,Test\n");
    }

    [Fact]
    public void Execute_WithDateTimeConversion_ShouldSucceed()
    {
        // Arrange
        var options = new DataTypeConvertOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Conversions = new List<ColumnTypeConversion>
        {
            new()
            {
                ColumnName = "DateValue",
                TargetType = DataType.DateTime,
                DateTimeFormat = "MM/dd/yyyy",  // 출력 형식
                Culture = CultureInfo.InvariantCulture
            }
        }
        };

        var task = new DataTypeConvertTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act & Assert
        Assert.True(task.Execute(context));
        string[] lines = File.ReadAllLines(_testOutputPath);
        var values = lines.Skip(1)
            .Select(l => l.Split(',')[2])
            .ToArray();

        Assert.Equal("01/10/2024", values[0]);
        Assert.Equal("02/20/2024", values[1]);
        Assert.Equal("03/30/2024", values[2]);
    }

    [Fact]
    public void Execute_WithDecimalConversion_ShouldSucceed()
    {
        // Arrange
        var options = new DataTypeConvertOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Conversions = new List<ColumnTypeConversion>
        {
            new()
            {
                ColumnName = "IntValue",
                TargetType = DataType.Decimal,
                Culture = CultureInfo.InvariantCulture
            }
        }
        };

        var task = new DataTypeConvertTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act & Assert
        Assert.True(task.Execute(context));
        string[] lines = File.ReadAllLines(_testOutputPath);
        var values = lines.Skip(1)
            .Select(l => l.Split(',')[0])
            .ToArray();

        Assert.Equal("123.0", values[0]);
        Assert.Equal("456.0", values[1]);
        Assert.Equal("789.0", values[2]);
    }

    private void Execute_WithCustomCulture_ShouldSucceed()
    {
        // Arrange
        var germanCulture = CultureInfo.GetCultureInfo("de-DE");
        var cultureDataPath = Path.GetTempFileName();

        // 독일 문화권으로 명시적으로 설정
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = germanCulture;

            File.WriteAllText(cultureDataPath,
                "Value\n" +
                "1234,56\n" +
                "7890,12\n");

            var options = new DataTypeConvertOption
            {
                InputPath = _testInputPath,
                OutputPath = _testOutputPath,
                Conversions = new List<ColumnTypeConversion>
            {
                new()
                {
                    ColumnName = "Value",
                    TargetType = DataType.Decimal,
                    Culture = germanCulture
                }
            }
            };

            var task = new DataTypeConvertTask(_mockLogger.Object);
            var context = new TaskContext(options);

            // Act & Assert
            Assert.True(task.Execute(context));
            string[] lines = File.ReadAllLines(_testOutputPath);
            Assert.Equal("1234.56", lines[1]);
            Assert.Equal("7890.12", lines[2]);
        }
        finally
        {
            // 원래 문화권으로 복원
            Thread.CurrentThread.CurrentCulture = originalCulture;
            File.Delete(cultureDataPath);
        }
    }

    [Fact]
    public void Execute_WithIntegerConversion_ShouldSucceed()
    {
        // Arrange
        var options = new DataTypeConvertOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Conversions = new List<ColumnTypeConversion>
            {
                new()
                {
                    ColumnName = "DecimalValue",
                    TargetType = DataType.Integer
                }
            }
        };

        var task = new DataTypeConvertTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        var values = lines.Skip(1)
            .Select(l => l.Split(',')[1])
            .ToArray();

        Assert.Equal("123", values[0]); // 123.45 -> 123
        Assert.Equal("457", values[1]); // 456.78 -> 457 (반올림)
        Assert.Equal("789", values[2]); // 789.12 -> 789
    }

    [Fact]
    public void Execute_WithBooleanConversion_ShouldSucceed()
    {
        // Arrange
        var options = new DataTypeConvertOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Conversions = new List<ColumnTypeConversion>
            {
                new()
                {
                    ColumnName = "BoolValue",
                    TargetType = DataType.Boolean,
                    IgnoreCase = true
                }
            }
        };

        var task = new DataTypeConvertTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        var values = lines.Skip(1)
            .Select(l => l.Split(',')[3])
            .ToArray();

        Assert.Equal("True", values[0]);  // true -> True
        Assert.Equal("False", values[1]); // false -> False
        Assert.Equal("True", values[2]);  // yes -> True
    }

    [Fact]
    public void Execute_WithInvalidData_AndIgnoreErrors_ShouldUseDefaultValue()
    {
        // Arrange
        var invalidDataPath = Path.GetTempFileName();
        File.WriteAllText(invalidDataPath,
            "Value\n" +
            "123\n" +
            "invalid\n" +
            "456\n");

        var options = new DataTypeConvertOption
        {
            InputPath = invalidDataPath,
            OutputPath = _testOutputPath,
            Conversions = new List<ColumnTypeConversion>
            {
                new()
                {
                    ColumnName = "Value",
                    TargetType = DataType.Integer,
                    DefaultValue = "-1"
                }
            },
            IgnoreErrors = true
        };

        var task = new DataTypeConvertTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Equal("123", lines[1]);
        Assert.Equal("-1", lines[2]); // 잘못된 데이터는 기본값으로 대체됨
        Assert.Equal("456", lines[3]);

        // Cleanup
        File.Delete(invalidDataPath);
    }

    [Fact]
    public void Execute_WithInvalidData_AndNoIgnoreErrors_ShouldFail()
    {
        // Arrange
        var invalidDataPath = Path.GetTempFileName();
        File.WriteAllText(invalidDataPath,
            "Value\n" +
            "123\n" +
            "invalid\n");

        var options = new DataTypeConvertOption
        {
            InputPath = invalidDataPath,
            OutputPath = _testOutputPath,
            Conversions = new List<ColumnTypeConversion>
        {
            new()
            {
                ColumnName = "Value",
                TargetType = DataType.Integer
            }
        },
            IgnoreErrors = false
        };

        var task = new DataTypeConvertTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => task.Execute(context));
        Assert.Contains("Invalid integer value: invalid", exception.Message);

        // Cleanup
        File.Delete(invalidDataPath);
    }

    [Fact]
    public void Execute_WithMultipleConverters_ShouldSucceed()
    {
        // Arrange
        var options = new DataTypeConvertOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Conversions = new List<ColumnTypeConversion>
            {
                new()
                {
                    ColumnName = "IntValue",
                    TargetType = DataType.String
                },
                new()
                {
                    ColumnName = "DecimalValue",
                    TargetType = DataType.Integer
                }
            }
        };

        var task = new DataTypeConvertTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        var firstRow = lines[1].Split(',');
        Assert.Equal("123", firstRow[0]); // string으로 변환된 IntValue
        Assert.Equal("123", firstRow[1]); // integer로 변환된 DecimalValue
    }

    [Fact]
    public void Execute_WithEmptyInput_ShouldSucceed()
    {
        // Arrange
        var emptyInputPath = Path.GetTempFileName();
        File.WriteAllText(emptyInputPath, "Value\n");

        var options = new DataTypeConvertOption
        {
            InputPath = emptyInputPath,
            OutputPath = _testOutputPath,
            Conversions = new List<ColumnTypeConversion>
            {
                new()
                {
                    ColumnName = "Value",
                    TargetType = DataType.Integer,
                    DefaultValue = "0"
                }
            }
        };

        var task = new DataTypeConvertTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Single(lines); // 헤더만 있어야 함

        // Cleanup
        File.Delete(emptyInputPath);
    }

    [Fact]
    public void Validate_WithInvalidDefaultValue_ShouldReturnError()
    {
        // Arrange
        var options = new DataTypeConvertOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Conversions = new List<ColumnTypeConversion>
            {
                new()
                {
                    ColumnName = "Value",
                    TargetType = DataType.Integer,
                    DefaultValue = "not-a-number"
                }
            }
        };

        // Act
        string[] errors = options.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("Invalid default value"));
    }

    [Fact]
    public void Validate_WithMissingDateTimeFormat_ShouldReturnError()
    {
        // Arrange
        var options = new DataTypeConvertOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Conversions = new List<ColumnTypeConversion>
            {
                new()
                {
                    ColumnName = "Value",
                    TargetType = DataType.DateTime,
                    DateTimeFormat = null
                }
            }
        };

        // Act
        string[] errors = options.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("DateTime format must be specified"));
    }

    [Fact]
    public void Validate_WithNoConversions_ShouldReturnError()
    {
        // Arrange
        var options = new DataTypeConvertOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Conversions = new List<ColumnTypeConversion>()
        };

        // Act
        string[] errors = options.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("At least one column conversion must be specified"));
    }

    [Fact]
    public void Validate_WithEmptyColumnName_ShouldReturnError()
    {
        // Arrange
        var options = new DataTypeConvertOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Conversions = new List<ColumnTypeConversion>
            {
                new()
                {
                    ColumnName = "",
                    TargetType = DataType.Integer
                }
            }
        };

        // Act
        string[] errors = options.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("Column name cannot be empty"));
    }
}