using FilePrepper.Tasks.BasicStatistics;
using FilePrepper.Tasks;
using Moq;
using Xunit.Abstractions;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace FilePrepper.Tests.Tasks;

public class BasicStatisticsTests : TaskBaseTest<BasicStatisticsTask>
{
    public BasicStatisticsTests(ITestOutputHelper output) : base(output)
    {
        // 테스트 입력 파일 생성
        File.WriteAllText(_testInputPath,
            "ID,Score,Grade,Value\n" +
            "1,85,A,100\n" +
            "2,92,A,150\n" +
            "3,78,B,80\n" +
            "4,95,A,200\n" +
            "5,88,B,120\n" +
            "6,72,C,90\n" +
            "7,98,A,180\n" +
            "8,82,B,110\n" +
            "9,90,A,160\n" +
            "10,75,C,95\n");
    }

    [Fact]
    public void Execute_WithInvalidNumericData_ShouldHandleGracefully()
    {
        // Arrange - 잘못된 숫자 데이터가 포함된 파일 생성
        var invalidDataPath = Path.GetTempFileName();
        File.WriteAllText(invalidDataPath,
            "ID,Value\n" +
            "1,100\n" +
            "2,invalid\n" +  // 잘못된 데이터
            "3,98\n" +
            "4,N/A\n" +     // 잘못된 데이터
            "5,101\n");

        var options = new BasicStatisticsOption
        {
            InputPath = invalidDataPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Value" },
            Statistics = new[] {
                StatisticType.Mean,
                StatisticType.StandardDeviation
            }
        };

        var task = new BasicStatisticsTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        Assert.True(result);
        Assert.True(File.Exists(_testOutputPath));

        // 결과 파일 내용 출력
        _output.WriteLine("=== Output File Contents ===");
        var outputLines = File.ReadAllLines(_testOutputPath);
        foreach (var line in outputLines)
        {
            _output.WriteLine(line);
        }

        File.Delete(invalidDataPath);
    }

    [Fact]
    public void Execute_WithRobustZScore_ShouldHandleOutliers()
    {
        // Arrange - 이상치가 포함된 데이터로 새 파일 생성
        var outlierDataPath = Path.GetTempFileName();
        File.WriteAllText(outlierDataPath,
            "ID,Value\n" +
            "1,100\n" +
            "2,102\n" +
            "3,98\n" +
            "4,103\n" +
            "5,1000\n" + // 극단적 이상치
            "6,97\n" +
            "7,101\n");

        var options = new BasicStatisticsOption
        {
            InputPath = outlierDataPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Value" },
            Statistics = new[] {
                StatisticType.ZScore,
                StatisticType.RobustZScore
            }
        };

        var task = new BasicStatisticsTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        Assert.True(result);
        Assert.True(File.Exists(_testOutputPath));

        // 결과 파일 내용 출력
        _output.WriteLine("=== Output File Contents ===");
        var outputLines = File.ReadAllLines(_testOutputPath);
        foreach (var line in outputLines)
        {
            _output.WriteLine(line);
        }

        // 이상치(1000)를 포함한 행에서 ZScore와 RobustZScore 비교
        var outlierLine = outputLines.First(l => l.Contains("1000"));
        var values = outlierLine.Split(',');
        var zScore = Math.Abs(double.Parse(values[^2], CultureInfo.InvariantCulture));
        var robustZScore = Math.Abs(double.Parse(values[^1], CultureInfo.InvariantCulture));

        _output.WriteLine($"ZScore: {zScore}");
        _output.WriteLine($"RobustZScore: {robustZScore}");

        Assert.True(robustZScore < zScore,
            $"RobustZScore ({robustZScore}) should be less than ZScore ({zScore}) for outliers");

        File.Delete(outlierDataPath);
    }

    [Fact]
    public void Execute_WithBasicStatistics_ShouldSucceed()
    {
        // Arrange
        var options = new BasicStatisticsOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Score" },
            Statistics = new[] {
                StatisticType.Mean,
                StatisticType.StandardDeviation,
                StatisticType.Min,
                StatisticType.Max,
                StatisticType.Median
            }
        };

        var task = new BasicStatisticsTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Contains("Score_stat_Mean", lines[0]);
        Assert.Contains("Score_stat_StandardDeviation", lines[0]);
        Assert.Contains("Score_stat_Min", lines[0]);
        Assert.Contains("Score_stat_Max", lines[0]);
        Assert.Contains("Score_stat_Median", lines[0]);

        _output.WriteLine("=== Output File Contents ===");
        foreach (var line in lines)
        {
            _output.WriteLine(line);
        }
    }

    [Fact]
    public void Execute_WithZScore_ShouldCalculateCorrectly()
    {
        // Arrange
        var options = new BasicStatisticsOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Score" },
            Statistics = new[] { StatisticType.ZScore }
        };

        var task = new BasicStatisticsTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Assert
        bool result = task.Execute(context);

        // Act
        Assert.True(result);

        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Contains("Score_stat_ZScore", lines[0]);

        // Z-Score 값들이 평균 0, 표준편차 1에 근접한지 확인
        var zScores = lines.Skip(1)
            .Select(l => double.Parse(l.Split(',').Last(), CultureInfo.InvariantCulture))
            .ToList();

        var meanZScore = zScores.Average();
        var stdZScore = Math.Sqrt(zScores.Average(x => Math.Pow(x - meanZScore, 2)));

        Assert.True(Math.Abs(meanZScore) < 0.0001); // 평균이 0에 가까운지 확인
        Assert.True(Math.Abs(stdZScore - 1.0) < 0.0001); // 표준편차가 1에 가까운지 확인
    }

    [Fact]
    public void Execute_WithPercentRank_ShouldCalculateCorrectly()
    {
        // Arrange
        var options = new BasicStatisticsOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Score" },
            Statistics = new[] { StatisticType.PercentRank }
        };

        var task = new BasicStatisticsTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Contains("Score_stat_PercentRank", lines[0]);

        var ranks = lines.Skip(1)
            .Select(l => double.Parse(l.Split(',').Last(), CultureInfo.InvariantCulture))
            .ToList();

        Assert.True(ranks.All(r => r >= 0 && r <= 100)); // 백분위 범위 확인
        Assert.Contains(100.0, ranks); // 최대값의 백분위는 100
        Assert.Contains(0.0, ranks);   // 최소값의 백분위는 0
    }

    [Fact]
    public void Execute_WithQuartiles_ShouldCalculateCorrectly()
    {
        // Arrange
        var options = new BasicStatisticsOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Score" },
            Statistics = new[] {
                StatisticType.Q1,
                StatisticType.Median,
                StatisticType.Q3
            }
        };

        var task = new BasicStatisticsTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);

        // Q1 < Median < Q3 확인
        var headerParts = lines[0].Split(',');
        var q1Index = Array.IndexOf(headerParts, "Score_stat_Q1");
        var medianIndex = Array.IndexOf(headerParts, "Score_stat_Median");
        var q3Index = Array.IndexOf(headerParts, "Score_stat_Q3");

        foreach (var line in lines.Skip(1))
        {
            var parts = line.Split(',');
            var q1 = double.Parse(parts[q1Index], CultureInfo.InvariantCulture);
            var median = double.Parse(parts[medianIndex], CultureInfo.InvariantCulture);
            var q3 = double.Parse(parts[q3Index], CultureInfo.InvariantCulture);

            Assert.True(q1 <= median);
            Assert.True(median <= q3);
        }
    }

    [Fact]
    public void Execute_WithMultipleColumns_ShouldCalculateAllStatistics()
    {
        // Arrange
        var options = new BasicStatisticsOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Score", "Value" },
            Statistics = new[] {
                StatisticType.Mean,
                StatisticType.StandardDeviation,
                StatisticType.ZScore
            }
        };

        var task = new BasicStatisticsTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);

        // 각 컬럼에 대한 통계량이 모두 계산되었는지 확인
        Assert.Contains("Score_stat_Mean", lines[0]);
        Assert.Contains("Score_stat_StandardDeviation", lines[0]);
        Assert.Contains("Score_stat_ZScore", lines[0]);
        Assert.Contains("Value_stat_Mean", lines[0]);
        Assert.Contains("Value_stat_StandardDeviation", lines[0]);
        Assert.Contains("Value_stat_ZScore", lines[0]);
    }
}