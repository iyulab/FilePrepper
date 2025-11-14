using FilePrepper.Tasks.DataSampling;
using FilePrepper.Tasks;
using Xunit.Abstractions;

namespace FilePrepper.Tests.Tasks;

public class DataSamplingTests : TaskBaseTest<DataSamplingTask>
{
    public DataSamplingTests(ITestOutputHelper output) : base(output)
    {
        // 테스트 입력 파일 생성
        File.WriteAllText(_testInputPath,
            "Id,Category,Value\n" +
            "1,A,100\n" +
            "2,A,200\n" +
            "3,B,300\n" +
            "4,B,400\n" +
            "5,C,500\n" +
            "6,C,600\n" +
            "7,A,700\n" +
            "8,B,800\n" +
            "9,C,900\n" +
            "10,A,1000\n");
    }

    [Fact]
    public void Execute_WithRandomSampling_ShouldSucceed()
    {
        // Arrange
        var options = new DataSamplingOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Method = SamplingMethod.Random,
            SampleSize = 0.5,  // 50%
            Seed = 42  // 재현성을 위한 시드값
        };

        var task = new DataSamplingTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Equal(6, lines.Length); // 헤더 + 5개 샘플 (50% of 10)
        Assert.Equal("Id,Category,Value", lines[0]); // 헤더 확인
    }

    [Fact]
    public void Execute_WithSystematicSampling_ShouldSucceed()
    {
        // Arrange
        var options = new DataSamplingOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Method = SamplingMethod.Systematic,
            SampleSize = 3,
            SystematicInterval = 3
        };

        var task = new DataSamplingTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Equal(5, lines.Length); // 헤더 + 4개 샘플 (1, 4, 7, 10번째 행)
    }

    [Fact]
    public void Execute_WithStratifiedSampling_ShouldSucceed()
    {
        // Arrange
        var options = new DataSamplingOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Method = SamplingMethod.Stratified,
            SampleSize = 0.5,  // 각 그룹에서 50%
            StratifyColumn = "Category",
            Seed = 42
        };

        var task = new DataSamplingTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);

        // 각 카테고리에서 최소 1개 이상 샘플링되었는지 확인
        var categories = lines.Skip(1)
            .Select(l => l.Split(',')[1])
            .Distinct()
            .ToList();
        Assert.Equal(3, categories.Count); // A, B, C 모두 포함
    }

    [Fact]
    public void Execute_WithExactSampleSize_ShouldSucceed()
    {
        // Arrange
        var options = new DataSamplingOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Method = SamplingMethod.Random,
            SampleSize = 3,  // 정확히 3개 샘플
            Seed = 42
        };

        var task = new DataSamplingTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Equal(4, lines.Length); // 헤더 + 3개 샘플
    }

    [Fact]
    public void Execute_WithSeed_ShouldProduceSameResults()
    {
        // Arrange
        var options1 = new DataSamplingOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Method = SamplingMethod.Random,
            SampleSize = 5,
            Seed = 42
        };

        var outputPath2 = Path.GetTempFileName();
        var options2 = new DataSamplingOption
        {
            InputPath = _testInputPath,
            OutputPath = outputPath2,
            Method = SamplingMethod.Random,
            SampleSize = 5,
            Seed = 42
        };

        var task1 = new DataSamplingTask(_mockLogger.Object);
        var task2 = new DataSamplingTask(_mockLogger.Object);

        var context1 = new TaskContext(options1);
        var context2 = new TaskContext(options2);

        // Act
        task1.Execute(context1);
        task2.Execute(context2);

        // Assert
        string[] result1 = File.ReadAllLines(_testOutputPath);
        string[] result2 = File.ReadAllLines(outputPath2);
        Assert.Equal(result1, result2); // 동일한 시드값을 사용하면 동일한 결과가 나와야 함

        // Cleanup
        File.Delete(outputPath2);
    }

    [Fact]
    public void Execute_WithTooLargeSampleSize_ShouldLimitToDataSize()
    {
        // Arrange
        var options = new DataSamplingOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Method = SamplingMethod.Random,
            SampleSize = 100, // 데이터 크기보다 큰 값
            Seed = 42
        };

        var task = new DataSamplingTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Equal(11, lines.Length); // 헤더 + 전체 데이터(10개)
    }

    [Fact]
    public void Execute_WithVerySmallRatio_ShouldSampleAtLeastOneRecord()
    {
        // Arrange
        var options = new DataSamplingOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Method = SamplingMethod.Random,
            SampleSize = 0.01, // 매우 작은 비율
            Seed = 42
        };

        var task = new DataSamplingTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.True(lines.Length >= 2); // 헤더 + 최소 1개 이상의 레코드
    }

    [Fact]
    public void Validate_WithInvalidSampleSize_ShouldReturnError()
    {
        // Arrange
        var options = new DataSamplingOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Method = SamplingMethod.Random,
            SampleSize = -1  // 잘못된 샘플 크기
        };

        // Act
        string[] errors = options.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("Sample size must be greater than 0"));
    }

    [Fact]
    public void Validate_WithStratifiedSamplingAndNoColumn_ShouldReturnError()
    {
        // Arrange
        var options = new DataSamplingOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Method = SamplingMethod.Stratified,
            SampleSize = 0.5,
            StratifyColumn = null  // 층화 컬럼이 지정되지 않음
        };

        // Act
        string[] errors = options.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("Stratify column must be specified for stratified sampling"));
    }

    [Fact]
    public void Validate_WithSystematicSamplingAndNoInterval_ShouldReturnError()
    {
        // Arrange
        var options = new DataSamplingOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Method = SamplingMethod.Systematic,
            SampleSize = 0.5,
            SystematicInterval = null  // 간격이 지정되지 않음
        };

        // Act
        string[] errors = options.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("Systematic interval must be greater than 0"));
    }

    [Fact]
    public void Validate_WithSystematicSamplingAndInvalidInterval_ShouldReturnError()
    {
        // Arrange
        var options = new DataSamplingOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Method = SamplingMethod.Systematic,
            SampleSize = 0.5,
            SystematicInterval = 0  // 잘못된 간격
        };

        // Act
        string[] errors = options.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("Systematic interval must be greater than 0"));
    }

    [Fact]
    public void Execute_WithEmptyInput_ShouldSucceed()
    {
        // Arrange - 빈 입력 파일 생성
        var emptyInputPath = Path.GetTempFileName();
        File.WriteAllText(emptyInputPath, "Id,Category,Value\n"); // 헤더만 있는 파일

        var options = new DataSamplingOption
        {
            InputPath = emptyInputPath,
            OutputPath = _testOutputPath,
            Method = SamplingMethod.Random,
            SampleSize = 0.5,
            Seed = 42
        };

        var task = new DataSamplingTask(_mockLogger.Object);
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
    public void Execute_WithStratifiedSampling_ShouldMaintainProportions()
    {
        // Arrange - 불균형한 그룹이 있는 데이터 생성
        var stratifiedInputPath = Path.GetTempFileName();
        File.WriteAllText(stratifiedInputPath,
            "Id,Category,Value\n" +
            "1,A,100\n" +
            "2,A,200\n" +
            "3,A,300\n" +
            "4,A,400\n" +
            "5,B,500\n" +
            "6,B,600\n");

        var options = new DataSamplingOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Method = SamplingMethod.Stratified,
            SampleSize = 0.5,
            StratifyColumn = "Category",
            Seed = 42
        };

        var task = new DataSamplingTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);

        // 각 카테고리별 샘플 수 확인
        var categoryCounts = lines.Skip(1)
            .Select(l => l.Split(',')[1])
            .GroupBy(c => c)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.True(categoryCounts["A"] >= 2); // A그룹(4개)에서 2개 이상
        Assert.True(categoryCounts["B"] >= 1); // B그룹(2개)에서 1개 이상

        // Cleanup
        File.Delete(stratifiedInputPath);
    }
}