using FilePrepper.Tasks.DropDuplicates;
using Moq;
using FilePrepper.Tasks;
using Xunit.Abstractions;

namespace FilePrepper.Tests.Tasks;

public class DropDuplicatesTests : TaskBaseTest<DropDuplicatesTask>
{
    public DropDuplicatesTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void Execute_WithAllColumnsDuplicates_KeepFirst_ShouldSucceed()
    {
        // Arrange
        File.WriteAllText(_testInputPath,
            "Id,Name,Value\n" +
            "1,John,100\n" +
            "2,Jane,200\n" +
            "1,John,100\n" +  // Duplicate of first row
            "3,Bob,300\n");

        var options = new DropDuplicatesOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            KeepFirst = true,
            SubsetColumnsOnly = false,
        };

        var task = new DropDuplicatesTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Equal(4, lines.Length); // Header + 3 unique records
        Assert.Contains("1,John,100", lines.Skip(1));  // First occurrence should be kept
        Assert.Contains("2,Jane,200", lines);
        Assert.Contains("3,Bob,300", lines);
    }

    [Fact]
    public void Execute_WithAllColumnsDuplicates_KeepLast_ShouldSucceed()
    {
        // Arrange
        File.WriteAllText(_testInputPath,
            "Id,Name,Value\n" +
            "1,John,100\n" +
            "2,Jane,200\n" +
            "1,John,100\n" +  // Duplicate of first row
            "3,Bob,300\n");

        var options = new DropDuplicatesOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            KeepFirst = false,
            SubsetColumnsOnly = false,
        };

        var task = new DropDuplicatesTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Equal(4, lines.Length); // Header + 3 unique records
        Assert.Contains("1,John,100", lines);  // Last occurrence should be kept
        Assert.Contains("2,Jane,200", lines);
        Assert.Contains("3,Bob,300", lines);
    }

    [Fact]
    public void Execute_WithSubsetColumnsDuplicates_ShouldSucceed()
    {
        // Arrange
        File.WriteAllText(_testInputPath,
            "Id,Name,Value\n" +
            "1,John,100\n" +
            "2,John,200\n" +  // Duplicate Name only
            "3,Jane,300\n");

        var options = new DropDuplicatesOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            KeepFirst = true,
            SubsetColumnsOnly = true,
            TargetColumns = new[] { "Name" },
        };

        var task = new DropDuplicatesTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Equal(3, lines.Length); // Header + 2 records (unique names)
        Assert.Contains("1,John,100", lines);  // First John should be kept
        Assert.DoesNotContain("2,John,200", lines);  // Second John should be removed
        Assert.Contains("3,Jane,300", lines);
    }

    [Fact]
    public void Execute_WithEmptyInput_ShouldSucceed()
    {
        // Arrange
        File.WriteAllText(_testInputPath, "Id,Name,Value\n");  // Header only

        var options = new DropDuplicatesOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            KeepFirst = true,
            SubsetColumnsOnly = false,
        };

        var task = new DropDuplicatesTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Single(lines);  // Only header
    }

    [Fact]
    public void Execute_WithAllUnique_ShouldNotRemoveAny()
    {
        // Arrange
        File.WriteAllText(_testInputPath,
            "Id,Name,Value\n" +
            "1,John,100\n" +
            "2,Jane,200\n" +
            "3,Bob,300\n");

        var options = new DropDuplicatesOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            KeepFirst = true,
            SubsetColumnsOnly = false
        };

        var task = new DropDuplicatesTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Equal(4, lines.Length);  // Header + 3 records (all unique)
    }

    [Fact]
    public void Validate_WithSubsetColumnsAndNoTargetColumns_ShouldReturnError()
    {
        // Arrange
        var options = new DropDuplicatesOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            SubsetColumnsOnly = true,
            TargetColumns = Array.Empty<string>()
        };

        // Act
        string[] errors = options.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("Target columns must be specified"));
    }

    [Fact]
    public void Execute_WithMultipleColumnSubset_ShouldSucceed()
    {
        // Arrange
        File.WriteAllText(_testInputPath,
            "Id,Name,Department,Salary\n" +
            "1,John,IT,1000\n" +
            "2,Jane,IT,2000\n" +  // Duplicate Department only
            "3,Bob,HR,3000\n" +
            "4,Alice,HR,4000\n");  // Duplicate Department only

        var options = new DropDuplicatesOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            KeepFirst = true,
            SubsetColumnsOnly = true,
            TargetColumns = new[] { "Department" }
        };

        var task = new DropDuplicatesTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Equal(3, lines.Length); // Header + 2 records (unique departments)
        Assert.Contains("1,John,IT,1000", lines);  // First IT department
        Assert.Contains("3,Bob,HR,3000", lines);  // First HR department
        Assert.DoesNotContain("2,Jane,IT,2000", lines);  // Second IT department
        Assert.DoesNotContain("4,Alice,HR,4000", lines);  // Second HR department
    }
}