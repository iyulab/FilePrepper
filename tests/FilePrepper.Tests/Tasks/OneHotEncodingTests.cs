using FilePrepper.Tasks;
using FilePrepper.Tasks.OneHotEncoding;
using Xunit.Abstractions;

namespace FilePrepper.Tests.Tasks;

public class OneHotEncodingTests : TaskBaseTest<OneHotEncodingTask>
{
    public OneHotEncodingTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void Validate_NoTargetColumns_ShouldReturnError()
    {
        // Arrange
        var option = new OneHotEncodingOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = Array.Empty<string>()
        };

        // Act
        var errors = option.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("At least one target column must be specified"));
    }

    [Fact]
    public void Execute_SimpleOneHot_ColumnsEncoded()
    {
        // Arrange
        // We have a CSV with a "Color" column that is categorical:
        //   Red, Blue, Red => distinct categories: [Blue, Red] after sorting
        //   => new columns: Color_Blue, Color_Red
        WriteTestFileLines(
            "Id,Color,Value",
            "1,Red,100",
            "2,Blue,200",
            "3,Red,300"
        );

        var option = new OneHotEncodingOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Color" },
            DropFirst = false,
            KeepOriginalColumns = false
        };

        var task = new OneHotEncodingTask(_mockLogger.Object);
        var context = new TaskContext(option);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        var lines = ReadOutputFileLines();

        // Expected output columns:
        // Id,Value,Color_Blue,Color_Red (since we removed original "Color")
        Assert.Equal(4, lines.Length); // header + 3 rows

        Assert.Contains("Id,Value,Color_Blue,Color_Red", lines[0]);

        // 1,Red => Blue=0, Red=1
        Assert.Contains("1,100,0,1", lines[1]);
        // 2,Blue => Blue=1, Red=0
        Assert.Contains("2,200,1,0", lines[2]);
        // 3,Red => Blue=0, Red=1
        Assert.Contains("3,300,0,1", lines[3]);
    }

    [Fact]
    public void Execute_KeepOriginalColumns_True()
    {
        // Arrange
        WriteTestFileLines(
            "Id,Color",
            "1,Red",
            "2,Blue"
        );

        var option = new OneHotEncodingOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Color" },
            DropFirst = false,
            KeepOriginalColumns = true
        };

        var task = new OneHotEncodingTask(_mockLogger.Object);
        var context = new TaskContext(option);

        // Act
        bool success = task.Execute(context);

        // Assert
        Assert.True(success);

        var lines = ReadOutputFileLines();
        Assert.Equal(3, lines.Length); // 1 header + 2 data
        Assert.Contains("Id,Color,Color_Blue,Color_Red", lines[0]);
        // row1 => Red => Blue=0, Red=1
        Assert.Contains("1,Red,0,1", lines[1]);
        // row2 => Blue => Blue=1, Red=0
        Assert.Contains("2,Blue,1,0", lines[2]);
    }

    [Fact]
    public void Execute_DropFirstCategory()
    {
        // Arrange
        // Distinct categories => [Blue, Red] after sort => skip "Blue" if DropFirst
        // Only "Color_Red" is created
        WriteTestFileLines(
            "Id,Color",
            "1,Red",
            "2,Blue",
            "3,Red"
        );

        var option = new OneHotEncodingOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Color" },
            DropFirst = true,
            KeepOriginalColumns = false
        };

        var task = new OneHotEncodingTask(_mockLogger.Object);
        var context = new TaskContext(option);

        // Act
        bool success = task.Execute(context);

        // Assert
        Assert.True(success);
        var lines = ReadOutputFileLines();

        // We expect only "Color_Red" (Blue is dropped) 
        // and original "Color" is removed
        Assert.Contains("Id,Color_Red", lines[0]);
        // row1 => Red => 1
        Assert.Contains("1,1", lines[1]);
        // row2 => Blue => 0
        Assert.Contains("2,0", lines[2]);
        // row3 => Red => 1
        Assert.Contains("3,1", lines[3]);
    }

    [Fact]
    public void Execute_MultipleTargetColumns()
    {
        // Arrange
        // We have two columns: "Color" and "Shape"
        // Color => [Blue, Red], Shape => [Circle, Square]
        WriteTestFileLines(
            "Id,Color,Shape",
            "1,Red,Circle",
            "2,Blue,Square",
            "3,Red,Square"
        );

        var option = new OneHotEncodingOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = new[] { "Color", "Shape" },
            DropFirst = false,
            KeepOriginalColumns = false
        };

        var task = new OneHotEncodingTask(_mockLogger.Object);
        var context = new TaskContext(option);

        // Act
        bool success = task.Execute(context);

        // Assert
        Assert.True(success);

        var lines = ReadOutputFileLines();
        // After removing original columns, the header should contain:
        // Id, Color_Blue, Color_Red, Shape_Circle, Shape_Square
        Assert.Contains("Id,Color_Blue,Color_Red,Shape_Circle,Shape_Square", lines[0]);

        // row1 => Red, Circle => Blue=0, Red=1, Circle=1, Square=0
        Assert.Contains("1,0,1,1,0", lines[1]);
        // row2 => Blue, Square => Blue=1, Red=0, Circle=0, Square=1
        Assert.Contains("2,1,0,0,1", lines[2]);
        // row3 => Red, Square => Blue=0, Red=1, Circle=0, Square=1
        Assert.Contains("3,0,1,0,1", lines[3]);
    }
}
