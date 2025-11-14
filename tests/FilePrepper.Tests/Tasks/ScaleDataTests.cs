using FilePrepper.Tasks.ScaleData;
using FilePrepper.Tasks;
using Xunit.Abstractions;

namespace FilePrepper.Tests.Tasks;

public class ScaleDataTests : TaskBaseTest<ScaleDataTask>
{
    public ScaleDataTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void Validate_NoScaleColumns_ShouldReturnError()
    {
        // Arrange
        var option = new ScaleDataOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Col1" },
            ScaleColumns = { }
        };

        // Act
        var errors = option.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("At least one column must be specified for scaling"));
    }

    [Fact]
    public void Validate_EmptyColumnName_ShouldReturnError()
    {
        // Arrange
        var option = new ScaleDataOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Col1" },
            ScaleColumns = { new ScaleColumnOption { ColumnName = "", Method = ScaleMethod.MinMax } }
        };

        // Act
        var errors = option.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("Column name cannot be empty or whitespace"));
    }

    [Fact]
    public void Execute_MinMaxScaling_ShouldSucceed()
    {
        // Arrange
        WriteTestFileLines(
            "Id,Value",
            "1,10",
            "2,20",
            "3,30"
        );

        var options = new ScaleDataOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Value" },
            ScaleColumns = {
                new ScaleColumnOption {
                    ColumnName = "Value",
                    Method = ScaleMethod.MinMax
                }
            }
        };

        var task = new ScaleDataTask(_mockLogger.Object);
        var context = new TaskContext(options);
        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        var lines = ReadOutputFileLines();
        // 헤더 + 3행 = 4라인
        Assert.Equal(4, lines.Length);
        // MinMax scaling: (value - 10) / (30 - 10)
        // 10 -> 0, 20 -> 0.5, 30 -> 1
        Assert.Contains("1,0", lines[1]);
        Assert.Contains("2,0.5", lines[2]);
        Assert.Contains("3,1", lines[3]);
    }

    [Fact]
    public void Execute_StandardizationScaling_ShouldSucceed()
    {
        // Arrange
        WriteTestFileLines(
            "Id,Value",
            "1,10",
            "2,20",
            "3,30"
        );

        var options = new ScaleDataOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Value" },
            ScaleColumns = {
            new ScaleColumnOption {
                ColumnName = "Value",
                Method = ScaleMethod.Standardization
            }
        }
        };

        var task = new ScaleDataTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        var lines = ReadOutputFileLines();
        Assert.Equal(4, lines.Length);

        // 예상 헤더 검증
        Assert.Equal("Id,Value", lines[0]);

        // 표준화 테스트 데이터 검증: 평균(mean) = 20, 표준편차(std) ≈ 8.1649658
        // (10-20)/8.1649658 ≈ -1.224745, (20-20)/8.1649658 = 0, (30-20)/8.1649658 ≈ 1.224745
        string[] expectedData = new[]
        {
            "1,-1.224745",
            "2,0",
            "3,1.224745"
        };

        // 데이터 행 비교 (헤더 제외하고 각 행별로 비교)
        for (int i = 1; i < lines.Length; i++)
        {
            var expectedParts = expectedData[i - 1].Split(',');
            var actualParts = lines[i].Split(',');

            // Id 컬럼 검증
            Assert.Equal(expectedParts[0], actualParts[0]);

            // Value 컬럼 검증 (소수점 5자리까지 비교)
            double expectedVal = double.Parse(expectedParts[1], System.Globalization.CultureInfo.InvariantCulture);
            double actualVal = double.Parse(actualParts[1], System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(expectedVal, actualVal, 5);
        }
    }


    [Fact]
    public void Execute_NonNumericValues_ShouldHandleGracefully()
    {
        // Arrange: Value 컬럼에 숫자와 비숫자 혼합
        WriteTestFileLines(
            "Id,Value",
            "1,10",
            "2,N/A",
            "3,30"
        );

        var options = new ScaleDataOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Value" },
            ScaleColumns = {
                new ScaleColumnOption {
                    ColumnName = "Value",
                    Method = ScaleMethod.MinMax
                }
            }
        };

        var task = new ScaleDataTask(_mockLogger.Object);
        var context = new TaskContext(options);
        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        var lines = ReadOutputFileLines();
        // N/A 값은 스케일링 적용 안됨, 따라서 그대로 남음
        // MinMax scaling 적용 대상: 10과 30
        // 10 -> 0, 30 -> 1
        Assert.Contains("1,0", lines[1]);
        Assert.Contains("2,N/A", lines[2]);
        Assert.Contains("3,1", lines[3]);
    }
}
