using Xunit.Abstractions;
using FilePrepper.Tasks.FileFormatConvert;
using FilePrepper.Tasks;
using Moq;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using FilePrepper.Tasks.FillMissingValues;

namespace FilePrepper.Tests.Tasks;

public class FileFormatConvertTests : TaskBaseTest<FileFormatConvertTask>
{
    public FileFormatConvertTests(ITestOutputHelper output) : base(output)
    {
        // 테스트 입력 파일 생성 (CSV 형식)
        File.WriteAllText(_testInputPath,
            "Name,Age,City\n" +
            "John Doe,30,New York\n" +
            "Jane Smith,25,Los Angeles\n" +
            "Bob Johnson,35,Chicago\n");
    }

    [Fact]
    public void Execute_ConvertToTSV_ShouldSucceed()
    {
        // Arrange
        var options = new FileFormatConvertOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetFormat = FileFormat.TSV,
            HasHeader = true,
            Encoding = Encoding.UTF8
        };

        var task = new FileFormatConvertTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Equal(4, lines.Length);
        Assert.Equal("Name\tAge\tCity", lines[0]);
        Assert.Equal("John Doe\t30\tNew York", lines[1]);
    }

    [Fact]
    public void Execute_ConvertToPSV_ShouldSucceed()
    {
        // Arrange
        var options = new FileFormatConvertOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetFormat = FileFormat.PSV,
            HasHeader = true
        };

        var task = new FileFormatConvertTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Equal(4, lines.Length);
        Assert.Equal("Name|Age|City", lines[0]);
        Assert.Equal("John Doe|30|New York", lines[1]);
    }

    [Fact]
    public void Execute_ConvertToJSON_ShouldSucceed()
    {
        // Arrange
        var options = new FileFormatConvertOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetFormat = FileFormat.JSON,
            PrettyPrint = true
        };

        var task = new FileFormatConvertTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string jsonContent = File.ReadAllText(_testOutputPath);
        var jsonData = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(jsonContent);

        Assert.NotNull(jsonData);
        Assert.Equal(3, jsonData.Count);
        Assert.Equal("John Doe", jsonData[0]["Name"]);
        Assert.Equal("30", jsonData[0]["Age"]);
        Assert.Equal("New York", jsonData[0]["City"]);
    }

    [Fact]
    public void Execute_ConvertToXML_ShouldSucceed()
    {
        // Arrange
        var options = new FileFormatConvertOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetFormat = FileFormat.XML,
            RootElementName = "data",
            ItemElementName = "record",
            PrettyPrint = true
        };

        var task = new FileFormatConvertTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        var xmlDoc = XDocument.Load(_testOutputPath);
        var records = xmlDoc.Root?.Elements("record").ToList();

        Assert.NotNull(records);
        Assert.Equal(3, records.Count);
        Assert.Equal("John Doe", records[0].Element("Name")?.Value);
        Assert.Equal("30", records[0].Element("Age")?.Value);
        Assert.Equal("New York", records[0].Element("City")?.Value);
    }

    [Fact]
    public void Execute_WithCustomEncoding_ShouldSucceed()
    {
        // Arrange
        var options = new FileFormatConvertOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetFormat = FileFormat.CSV,
            Encoding = Encoding.UTF32
        };

        var task = new FileFormatConvertTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        using var reader = new StreamReader(_testOutputPath, Encoding.UTF32);
        string content = reader.ReadToEnd();
        Assert.Contains("John Doe", content);
    }

    [Fact]
    public void Execute_WithoutHeader_ShouldSucceed()
    {
        // Arrange
        var options = new FileFormatConvertOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetFormat = FileFormat.CSV,
            HasHeader = false
        };

        var task = new FileFormatConvertTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Equal(3, lines.Length); // 헤더가 없으므로 3줄이어야 함
        Assert.StartsWith("John Doe", lines[0]); // 첫 줄이 데이터로 시작해야 함
    }

    [Fact]
    public void Validate_WithInvalidXMLNames_ShouldReturnError()
    {
        // Arrange
        var options = new FileFormatConvertOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetFormat = FileFormat.XML,
            RootElementName = "",
            ItemElementName = "invalid name"
        };

        // Act
        string[] errors = options.Validate();

        // Debug - Print actual error messages
        _output.WriteLine("Actual errors:");
        foreach (var error in errors)
        {
            _output.WriteLine($"- {error}");
        }

        // Assert
        Assert.Single(errors);
        Assert.Equal("root element name name cannot be empty or whitespace", errors[0]);
    }

    [Fact]
    public void Validate_WithDelimiterForPredefinedFormat_ShouldReturnError()
    {
        // Arrange
        var options = new FileFormatConvertOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetFormat = FileFormat.CSV,
            Delimiter = "|"
        };

        // Act
        string[] errors = options.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("Delimiter cannot be specified for CSV format"));
    }

    [Fact]
    public void Execute_WithEmptyInput_ShouldSucceed()
    {
        // Arrange
        var emptyInputPath = Path.GetTempFileName();
        File.WriteAllText(emptyInputPath, "Name,Age,City\n");

        var options = new FileFormatConvertOption
        {
            InputPath = emptyInputPath,
            OutputPath = _testOutputPath,
            TargetFormat = FileFormat.JSON,
            PrettyPrint = true
        };

        var task = new FileFormatConvertTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string jsonContent = File.ReadAllText(_testOutputPath);
        var jsonData = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(jsonContent);
        Assert.Empty(jsonData!);

        // Cleanup
        File.Delete(emptyInputPath);
    }
}