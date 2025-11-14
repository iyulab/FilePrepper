using FilePrepper.Tasks;
using FilePrepper.Tasks.NormalizeData;
using Xunit.Abstractions;

namespace FilePrepper.Tests.Tasks;

public class NormalizeDataTests : TaskBaseTest<NormalizeDataTask>
{
    public NormalizeDataTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void Validate_NoTargetColumns_ReturnsError()
    {
        // Arrange
        var option = new NormalizeDataOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = Array.Empty<string>() // no columns
        };

        // Act
        var errors = option.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("At least one target column"));
    }

    [Fact]
    public void Validate_MinMaxInvalidRange_ReturnsError()
    {
        // Arrange
        var option = new NormalizeDataOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Method = NormalizationMethod.MinMax,
            TargetColumns = new[] { "Score" },
            MinValue = 5,
            MaxValue = 5 // same => invalid
        };

        // Act
        var errors = option.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("MinValue must be less than MaxValue"));
    }

    [Fact]
    public void Validate_MinMaxValidRange_NoError()
    {
        // Arrange
        var option = new NormalizeDataOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Method = NormalizationMethod.MinMax,
            TargetColumns = new[] { "Score" },
            MinValue = 0,
            MaxValue = 1
        };

        // Act
        var errors = option.Validate();

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void Execute_MinMaxNormalization_ShouldScaleCorrectly()
    {
        // Arrange
        // CSV with numeric column Score
        // Score values: 10, 20 => min=10, max=20 => scaled to 0..1
        // => row1 => (10-10)/(20-10)=0 => final=0.0
        // => row2 => (20-10)/(20-10)=1 => final=1.0
        WriteTestFileLines(
            "Id,Score,Name",
            "1,10,Alice",
            "2,20,Bob"
        );

        var option = new NormalizeDataOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Method = NormalizationMethod.MinMax,
            TargetColumns = new[] { "Score" },
            MinValue = 0,
            MaxValue = 1
        };

        var task = new NormalizeDataTask(_mockLogger.Object);

        var context = new TaskContext(option);

        // Act
        bool success = task.Execute(context);

        // Assert
        Assert.True(success);

        var lines = ReadOutputFileLines();
        Assert.Equal(3, lines.Length); // header + 2 rows

        // Check headers
        Assert.Contains("Id,Score,Name", lines[0]);
        // Check normalized values
        Assert.Contains("1,0,Alice", lines[1]);
        Assert.Contains("2,1,Bob", lines[2]);
    }

    [Fact]
    public void Execute_ZScoreNormalization_ShouldTransformCorrectly()
    {
        // Arrange
        // CSV with numeric column Score
        // Score: 10, 20 => mean=15, std=5 => ZScore(10)= (10-15)/5 = -1, (20-15)/5=1
        WriteTestFileLines(
            "Id,Score,Name",
            "1,10,Alice",
            "2,20,Bob"
        );

        var option = new NormalizeDataOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Method = NormalizationMethod.ZScore,
            TargetColumns = new[] { "Score" }
        };

        var task = new NormalizeDataTask(_mockLogger.Object);

        var context = new TaskContext(option);

        // Act
        bool success = task.Execute(context);

        // Assert
        Assert.True(success);

        var lines = ReadOutputFileLines();
        Assert.Equal(3, lines.Length); // header + 2 rows

        Assert.Contains("Id,Score,Name", lines[0]);
        // We expect approximately -1 and 1 for the z-scores
        Assert.Contains("1,-1", lines[1]);
        Assert.Contains("2,1", lines[2]);
    }

    [Fact]
    public void Execute_MinMaxNormalization_ZeroVariance_ShouldSetToMinValue()
    {
        // Arrange
        // All values are the same => min=max=10 => everything = MinValue => 0
        WriteTestFileLines(
            "Id,Score",
            "1,10",
            "2,10"
        );

        var option = new NormalizeDataOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Method = NormalizationMethod.MinMax,
            TargetColumns = new[] { "Score" },
            MinValue = 0,
            MaxValue = 1
        };

        var task = new NormalizeDataTask(_mockLogger.Object);

        var context = new TaskContext(option);

        // Act
        bool success = task.Execute(context);

        // Assert
        Assert.True(success);

        var lines = ReadOutputFileLines();
        Assert.Equal(3, lines.Length); // header + 2 rows
        // Zero variance => all become MinValue=0
        Assert.Contains("1,0", lines[1]);
        Assert.Contains("2,0", lines[2]);
    }

    [Fact]
    public void Execute_ZScoreNormalization_ZeroVariance_ShouldSetToMean()
    {
        // Arrange
        // All values the same => mean=10, std=0 => we store them at the mean (10)
        WriteTestFileLines(
            "Id,Score",
            "1,10",
            "2,10"
        );

        var option = new NormalizeDataOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Method = NormalizationMethod.ZScore,
            TargetColumns = new[] { "Score" }
        };

        var task = new NormalizeDataTask(_mockLogger.Object);

        var context = new TaskContext(option);

        // Act
        bool success = task.Execute(context);

        // Assert
        Assert.True(success);

        var lines = ReadOutputFileLines();
        Assert.Equal(3, lines.Length); // header + 2 rows
        // Zero variance => set values to mean=10
        Assert.Contains("1,10", lines[1]);
        Assert.Contains("2,10", lines[2]);
    }

    [Fact]
    public void Execute_WithNonNumericData_IgnoreErrors_ShouldUseDefaultValue()
    {
        // Arrange
        // "NaN" in Score => if we set IgnoreErrors=true and DefaultValue="0", it should parse as 0
        WriteTestFileLines(
            "Id,Score",
            "1,10",
            "2,NaN"
        );

        var option = new NormalizeDataOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Method = NormalizationMethod.MinMax,
            TargetColumns = new[] { "Score" },
            MinValue = 0,
            MaxValue = 1,
            IgnoreErrors = true,
            DefaultValue = "0"
        };

        var task = new NormalizeDataTask(_mockLogger.Object);

        var context = new TaskContext(option);

        // Act
        bool success = task.Execute(context);

        // Assert
        Assert.True(success);

        var lines = ReadOutputFileLines();
        Assert.Equal(3, lines.Length);

        // Score: row1=10, row2=0 (after ignoring "NaN" and using default=0)
        // min=0, max=10 => row1 => (10-0)/10=1 => row2 => (0-0)/10=0
        Assert.Contains("Id,Score", lines[0]);
        Assert.Contains("1,1", lines[1]);
        Assert.Contains("2,0", lines[2]);
    }

    [Fact]
    public void Execute_NoNumericData_NoChange()
    {
        // Arrange
        // There's no numeric column in target => or the columns are not present
        WriteTestFileLines(
            "Id,Name",
            "1,Alice",
            "2,Bob"
        );

        var option = new NormalizeDataOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Method = NormalizationMethod.MinMax,
            TargetColumns = new[] { "Score" }, // This column doesn't exist in CSV
            MinValue = 0,
            MaxValue = 1
        };

        var task = new NormalizeDataTask(_mockLogger.Object);

        var context = new TaskContext(option);

        // Act
        bool success = task.Execute(context);

        // Assert
        Assert.True(success);

        var lines = ReadOutputFileLines();
        Assert.Equal(3, lines.Length); // header + 2 rows
        // The CSV is unchanged since "Score" column does not exist
        Assert.Contains("Id,Name", lines[0]);
        Assert.Contains("1,Alice", lines[1]);
        Assert.Contains("2,Bob", lines[2]);
    }
}
