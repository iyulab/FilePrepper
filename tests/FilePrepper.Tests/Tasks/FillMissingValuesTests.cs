using Xunit;
using FilePrepper.Tasks.FillMissingValues;
using FilePrepper.Tasks;
using Moq;
using Xunit.Abstractions;

namespace FilePrepper.Tests.Tasks;

public class FillMissingValuesTests : TaskBaseTest<FillMissingValuesTask>
{
    public FillMissingValuesTests(ITestOutputHelper output) : base(output)
    {
        WriteTestFileLines(
            "Id,Name,Age,Score",
            "1,John,,85.5",
            "2,,25,",
            "3,Mary,30,92.0",
            "4,Tom,,77.5",
            "5,,,88.0"
        );
    }

    [Fact]
    public void Execute_WithFixedValue_ShouldSucceed()
    {
        // Arrange
        var options = new FillMissingValuesOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Name" },
            FillMethods = new List<ColumnFillMethod>
            {
                new()
                {
                    ColumnName = "Name",
                    Method = FillMethod.FixedValue,
                    FixedValue = "Unknown"
                }
            }
        };

        var task = new FillMissingValuesTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        _output.WriteLine("Output file content:");
        if (File.Exists(_testOutputPath))
        {
            _output.WriteLine(File.ReadAllText(_testOutputPath));
        }
        else
        {
            _output.WriteLine("Output file was not created!");
        }

        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Equal("2,Unknown,25,", lines[2]);
        Assert.Equal("5,Unknown,,88.0", lines[5]);
    }

    [Fact]
    public void Execute_WithMean_ShouldSucceed()
    {
        // Arrange
        var options = new FillMissingValuesOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Age" },
            FillMethods = new List<ColumnFillMethod>
            {
                new()
                {
                    ColumnName = "Age",
                    Method = FillMethod.Mean
                }
            }
        };

        var task = new FillMissingValuesTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        _output.WriteLine("Output file content:");
        if (File.Exists(_testOutputPath))
        {
            _output.WriteLine(File.ReadAllText(_testOutputPath));
        }

        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Equal("1,John,27.5,85.5", lines[1]);
        Assert.Equal("4,Tom,27.5,77.5", lines[4]);
    }

    [Fact]
    public void Execute_WithMedian_ShouldSucceed()
    {
        // Arrange
        var options = new FillMissingValuesOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Score" },
            FillMethods = new List<ColumnFillMethod>
            {
                new()
                {
                    ColumnName = "Score",
                    Method = FillMethod.Median
                }
            }
        };

        var task = new FillMissingValuesTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        _output.WriteLine("Output file content:");
        if (File.Exists(_testOutputPath))
        {
            _output.WriteLine(File.ReadAllText(_testOutputPath));
        }

        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Contains("86.75", lines[2]); // 중앙값으로 채워져야 함
    }

    [Fact]
    public void Execute_WithMode_ShouldSucceed()
    {
        // Arrange
        var options = new FillMissingValuesOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Name" },
            FillMethods = new List<ColumnFillMethod>
            {
                new()
                {
                    ColumnName = "Name",
                    Method = FillMethod.Mode
                }
            }
        };

        var task = new FillMissingValuesTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        _output.WriteLine("Output file content:");
        if (File.Exists(_testOutputPath))
        {
            _output.WriteLine(File.ReadAllText(_testOutputPath));
        }

        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Contains("John", lines[2]); // 최빈값으로 채워져야 함
    }

    [Fact]
    public void Execute_WithForwardFill_ShouldSucceed()
    {
        // Arrange
        var options = new FillMissingValuesOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Name" },
            FillMethods = new List<ColumnFillMethod>
            {
                new()
                {
                    ColumnName = "Name",
                    Method = FillMethod.ForwardFill
                }
            }
        };

        var task = new FillMissingValuesTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        _output.WriteLine("Output file content:");
        if (File.Exists(_testOutputPath))
        {
            _output.WriteLine(File.ReadAllText(_testOutputPath));
        }

        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Equal("2,John,25,", lines[2]); // 앞의 값으로 채워져야 함
    }

    [Fact]
    public void Execute_WithBackwardFill_ShouldSucceed()
    {
        // Arrange
        var options = new FillMissingValuesOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Name" },
            FillMethods = new List<ColumnFillMethod>
            {
                new()
                {
                    ColumnName = "Name",
                    Method = FillMethod.BackwardFill
                }
            }
        };

        var task = new FillMissingValuesTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        _output.WriteLine("Output file content:");
        if (File.Exists(_testOutputPath))
        {
            _output.WriteLine(File.ReadAllText(_testOutputPath));
        }

        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Equal("2,Mary,25,", lines[2]); // 뒤의 값으로 채워져야 함
    }

    [Fact]
    public void Execute_WithLinearInterpolation_ShouldSucceed()
    {
        // Arrange
        WriteTestFileLines(
            "Id,Name,Age,Score",
            "1,John,20,85.5",   // 시작값 20
            "2,Jane,,92.0",     // 보간될 값
            "3,Mary,40,77.5"    // 끝값 40
        );

        var options = new FillMissingValuesOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Age" },
            FillMethods = new List<ColumnFillMethod>
        {
            new()
            {
                ColumnName = "Age",
                Method = FillMethod.LinearInterpolation
            }
        }
        };

        var task = new FillMissingValuesTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Equal("2,Jane,30,92.0", lines[2]); // 20과 40 사이의 중간값인 30
    }

    [Fact]
    public void Execute_WithNonNumericValues_ShouldHandleGracefully()
    {
        // Arrange
        WriteTestFileLines(
            "Id,Value",
            "1,100",
            "2,N/A",
            "3,200",
            "4,Error",
            "5,300"
        );

        var options = new FillMissingValuesOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            // 여기에 명시적으로 TargetColumns를 지정
            TargetColumns = new[] { "Value" },
            FillMethods = new List<ColumnFillMethod>
            {
                new()
                {
                    ColumnName = "Value",
                    Method = FillMethod.Mean
                }
            }
        };


        var task = new FillMissingValuesTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        _output.WriteLine("Output file content:");
        if (File.Exists(_testOutputPath))
        {
            _output.WriteLine(File.ReadAllText(_testOutputPath));
        }

        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Equal("2,200", lines[2]); // (100 + 200 + 300) / 3 = 200
    }

    [Fact]
    public void Execute_WithMultipleMethods_ShouldSucceed()
    {
        // Arrange
        var options = new FillMissingValuesOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Name", "Age" },
            FillMethods = new List<ColumnFillMethod>
            {
                new()
                {
                    ColumnName = "Name",
                    Method = FillMethod.FixedValue,
                    FixedValue = "Unknown"
                },
                new()
                {
                    ColumnName = "Age",
                    Method = FillMethod.Mean
                }
            }
        };

        var task = new FillMissingValuesTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        _output.WriteLine("Output file content:");
        if (File.Exists(_testOutputPath))
        {
            _output.WriteLine(File.ReadAllText(_testOutputPath));
        }

        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Equal("2,Unknown,25,", lines[2]);
        Assert.Equal("5,Unknown,27.5,88.0", lines[5]); // Name은 Unknown, Age는 평균값으로
    }

    [Fact]
    public void Validate_WithNoFillMethods_ShouldReturnError()
    {
        // Arrange
        var options = new FillMissingValuesOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Name" },
            FillMethods = new List<ColumnFillMethod>()
        };

        // Act
        string[] errors = options.Validate();

        // Assert
        _output.WriteLine("Validation errors:");
        foreach (var error in errors)
        {
            _output.WriteLine(error);
        }

        Assert.Contains(errors, e => e.Contains("At least one fill method must be specified"));
    }

    [Fact]
    public void Validate_WithEmptyColumnName_ShouldReturnError()
    {
        // Arrange
        var options = new FillMissingValuesOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "" },
            FillMethods = new List<ColumnFillMethod>
            {
                new()
                {
                    ColumnName = "",
                    Method = FillMethod.FixedValue,
                    FixedValue = "Unknown"
                }
            }
        };

        // Act
        string[] errors = options.Validate();

        // Assert
        _output.WriteLine("Validation errors:");
        foreach (var error in errors)
        {
            _output.WriteLine(error);
        }

        Assert.Contains(errors, e => e.Contains("Column name cannot be empty or whitespace"));
    }

    [Fact]
    public void Validate_WithFixedValueMethodButNoValue_ShouldReturnError()
    {
        // Arrange
        var options = new FillMissingValuesOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Name" },
            FillMethods = new List<ColumnFillMethod>
            {
                new()
                {
                    ColumnName = "Name",
                    Method = FillMethod.FixedValue
                }
            }
        };

        // Act
        string[] errors = options.Validate();

        // Assert
        _output.WriteLine("Validation errors:");
        foreach (var error in errors)
        {
            _output.WriteLine(error);
        }

        Assert.Contains(errors, e => e.Contains("Fixed value must be specified for column Name"));
    }

    [Fact]
    public void Execute_WithAllMissingValues_ShouldHandleGracefully()
    {
        // Arrange
        var testFile = Path.GetTempFileName();
        File.WriteAllText(testFile,
            "Id,Value\n" +
            "1,\n" +
            "2,\n" +
            "3,\n");

        var options = new FillMissingValuesOption
        {
            InputPath = testFile,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Value" },
            FillMethods = new List<ColumnFillMethod>
            {
                new()
                {
                    ColumnName = "Value",
                    Method = FillMethod.Mean
                }
            },
            DefaultValue = "0"
        };

        var task = new FillMissingValuesTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        _output.WriteLine("Output file content:");
        if (File.Exists(_testOutputPath))
        {
            _output.WriteLine(File.ReadAllText(_testOutputPath));
        }

        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        // 모든 값이 비어있을 경우 DefaultValue로 대체
        Assert.Equal("1,0", lines[1]);
        Assert.Equal("2,0", lines[2]);
        Assert.Equal("3,0", lines[3]);

        // Cleanup
        File.Delete(testFile);
    }

    [Fact]
    public void Execute_WithSingleRow_ShouldSucceed()
    {
        // Arrange
        var testFile = Path.GetTempFileName();
        File.WriteAllText(testFile,
            "Id,Value\n" +
            "1,\n");

        var options = new FillMissingValuesOption
        {
            InputPath = testFile,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Value" },
            FillMethods = new List<ColumnFillMethod>
            {
                new()
                {
                    ColumnName = "Value",
                    Method = FillMethod.FixedValue,
                    FixedValue = "Test"
                }
            }
        };

        var task = new FillMissingValuesTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        _output.WriteLine("Output file content:");
        if (File.Exists(_testOutputPath))
        {
            _output.WriteLine(File.ReadAllText(_testOutputPath));
        }

        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Equal(2, lines.Length); // 헤더 + 1행
        Assert.Equal("1,Test", lines[1]);

        // Cleanup
        File.Delete(testFile);
    }
}