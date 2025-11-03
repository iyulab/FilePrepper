using FilePrepper.Tasks.DateExtraction;
using Moq;
using FilePrepper.Tasks;
using Xunit.Abstractions;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace FilePrepper.Tests.Tasks;

public class DateExtractionTests : TaskBaseTest<DateExtractionTask>
{
    public DateExtractionTests(ITestOutputHelper output) : base(output)
    {
        // Create test input file
        File.WriteAllText(_testInputPath,
            "DateValue\n" +
            "2024-01-10 15:30:45\n" +
            "2024-02-20 08:15:30\n" +
            "2024-03-30 12:45:20\n");
    }

    [Fact]
    public void Execute_WithBasicExtraction_ShouldSucceed()
    {
        // Arrange
        var options = new DateExtractionOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Extractions = new List<DateColumnExtraction>
            {
                new()
                {
                    SourceColumn = "DateValue",
                    Components = new List<DateComponent>
                    {
                        DateComponent.Year,
                        DateComponent.Month,
                        DateComponent.Day
                    },
                    OutputColumnTemplate = "{column}_{component}"
                }
            },
            AppendToSource = false
        };

        var task = new DateExtractionTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        var headers = lines[0].Split(',');
        var firstRow = lines[1].Split(',');

        Assert.Contains("DateValue_Year", headers);
        Assert.Contains("DateValue_Month", headers);
        Assert.Contains("DateValue_Day", headers);

        Assert.Equal("2024", firstRow[headers.ToList().IndexOf("DateValue_Year")]);
        Assert.Equal("1", firstRow[headers.ToList().IndexOf("DateValue_Month")]);
        Assert.Equal("10", firstRow[headers.ToList().IndexOf("DateValue_Day")]);
    }

    [Fact]
    public void Execute_WithCustomFormat_ShouldSucceed()
    {
        // Arrange
        var customFormatPath = Path.GetTempFileName();
        File.WriteAllText(customFormatPath,
            "DateValue\n" +
            "10/Jan/2024\n" +
            "20/Feb/2024\n");

        var options = new DateExtractionOption
        {
            InputPath = customFormatPath,
            OutputPath = _testOutputPath,
            Extractions = new List<DateColumnExtraction>
            {
                new()
                {
                    SourceColumn = "DateValue",
                    DateFormat = "dd/MMM/yyyy",
                    Components = new List<DateComponent> { DateComponent.Month },
                    OutputColumnTemplate = "{column}_{component}"
                }
            },
            AppendToSource = false
        };

        var task = new DateExtractionTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        var headers = lines[0].Split(',');
        var firstRow = lines[1].Split(',');

        Assert.Contains("DateValue_Month", headers);
        Assert.Equal("1", firstRow[headers.ToList().IndexOf("DateValue_Month")]);

        // Cleanup
        File.Delete(customFormatPath);
    }

    [Fact]
    public void Execute_WithCustomCulture_ShouldSucceed()
    {
        // Arrange
        var germanCulture = CultureInfo.GetCultureInfo("de-DE");
        var culturePath = Path.GetTempFileName();
        File.WriteAllText(culturePath,
            "DateValue\n" +
            "10.01.2024\n" +
            "20.02.2024\n");

        var options = new DateExtractionOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Extractions = new List<DateColumnExtraction>
            {
                new()
                {
                    SourceColumn = "DateValue",
                    Culture = germanCulture,
                    Components = new List<DateComponent> { DateComponent.Month },
                    OutputColumnTemplate = "{column}_{component}"
                }
            },
            AppendToSource = false
        };

        var task = new DateExtractionTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        var headers = lines[0].Split(',');
        var firstRow = lines[1].Split(',');

        Assert.Contains("DateValue_Month", headers);
        Assert.Equal("1", firstRow[headers.ToList().IndexOf("DateValue_Month")]);

        // Cleanup
        File.Delete(culturePath);
    }

    [Fact]
    public void Execute_WithAllComponents_ShouldSucceed()
    {
        // Arrange
        var options = new DateExtractionOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Extractions = new List<DateColumnExtraction>
            {
                new()
                {
                    SourceColumn = "DateValue",
                    Components = Enum.GetValues<DateComponent>().ToList(),
                    OutputColumnTemplate = "{column}_{component}"
                }
            },
            AppendToSource = false
        };

        var task = new DateExtractionTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        var headers = lines[0].Split(',');
        var firstRow = lines[1].Split(',');

        foreach (var component in Enum.GetValues<DateComponent>())
        {
            Assert.Contains($"DateValue_{component}", headers);
            var value = firstRow[headers.ToList().IndexOf($"DateValue_{component}")];
            Assert.False(string.IsNullOrEmpty(value), $"Value for {component} should not be empty");
        }
    }

    [Fact]
    public void Execute_WithTimeComponents_ShouldSucceed()
    {
        // Arrange
        var options = new DateExtractionOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Extractions = new List<DateColumnExtraction>
            {
                new()
                {
                    SourceColumn = "DateValue",
                    Components = new List<DateComponent>
                    {
                        DateComponent.Hour,
                        DateComponent.Minute,
                        DateComponent.Second
                    },
                    OutputColumnTemplate = "{column}_{component}"
                }
            },
            AppendToSource = false
        };

        var task = new DateExtractionTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        var headers = lines[0].Split(',');
        var firstRow = lines[1].Split(',');

        Assert.Equal("15", firstRow[headers.ToList().IndexOf("DateValue_Hour")]);
        Assert.Equal("30", firstRow[headers.ToList().IndexOf("DateValue_Minute")]);
        Assert.Equal("45", firstRow[headers.ToList().IndexOf("DateValue_Second")]);
    }

    [Fact]
    public void Execute_WithInvalidDate_AndIgnoreErrors_ShouldSucceed()
    {
        // Arrange
        var invalidDataPath = Path.GetTempFileName();
        File.WriteAllText(invalidDataPath,
            "DateValue\n" +
            "2024-01-10\n" +
            "invalid-date\n" +
            "2024-03-30\n");

        var options = new DateExtractionOption
        {
            InputPath = invalidDataPath,
            OutputPath = _testOutputPath,
            Extractions = new List<DateColumnExtraction>
            {
                new()
                {
                    SourceColumn = "DateValue",
                    Components = new List<DateComponent> { DateComponent.Year },
                    OutputColumnTemplate = "{column}_{component}"
                }
            },
            AppendToSource = false,
            IgnoreErrors = true
        };

        var task = new DateExtractionTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);

        // 로그 확인
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Failed to parse date value")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // 출력 파일 확인
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Equal(4, lines.Length); // Header + 3 data rows

        // Cleanup
        File.Delete(invalidDataPath);
    }

    [Fact]
    public void Execute_WithInvalidDate_AndNoIgnoreErrors_ShouldFail()
    {
        // Arrange
        var invalidDataPath = Path.GetTempFileName();
        File.WriteAllText(invalidDataPath,
            "DateValue\n" +
            "2024-01-10\n" +
            "invalid-date\n");

        var options = new DateExtractionOption
        {
            InputPath = invalidDataPath,
            OutputPath = _testOutputPath,
            Extractions = new List<DateColumnExtraction>
        {
            new()
            {
                SourceColumn = "DateValue",
                Components = new List<DateComponent> { DateComponent.Year },
                OutputColumnTemplate = "{column}_{component}"
            }
        },
            AppendToSource = false,
            IgnoreErrors = false
        };

        var task = new DateExtractionTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => task.Execute(context));
        Assert.Contains("Failed to parse date value 'invalid-date'", exception.Message);

        // Cleanup
        File.Delete(invalidDataPath);
    }

    [Fact]
    public void Execute_WithEmptyInput_ShouldSucceed()
    {
        // Arrange
        var emptyInputPath = Path.GetTempFileName();
        File.WriteAllText(emptyInputPath, "DateValue\n");

        var options = new DateExtractionOption
        {
            InputPath = emptyInputPath,
            OutputPath = _testOutputPath,
            Extractions = new List<DateColumnExtraction>
            {
                new()
                {
                    SourceColumn = "DateValue",
                    Components = new List<DateComponent> { DateComponent.Year },
                    OutputColumnTemplate = "{column}_{component}"
                }
            },
            AppendToSource = false
        };

        var task = new DateExtractionTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        Assert.Single(lines); // Header only

        // Cleanup
        File.Delete(emptyInputPath);
    }

    [Fact]
    public void Execute_WithAppendToSource_ShouldSucceed()
    {
        // Arrange
        var options = new DateExtractionOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Extractions = new List<DateColumnExtraction>
            {
                new()
                {
                    SourceColumn = "DateValue",
                    Components = new List<DateComponent> { DateComponent.Year },
                }
            },
            AppendToSource = true,
            OutputColumnTemplate = "{column}_{component}"
        };

        var task = new DateExtractionTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        var headers = lines[0].Split(',');

        // Original column + new column
        Assert.Equal(2, headers.Length);
        Assert.Contains("DateValue", headers);
        Assert.Contains("DateValue_Year", headers);
    }

    [Fact]
    public void Execute_WithMultipleExtractions_ShouldSucceed()
    {
        // Arrange
        var options = new DateExtractionOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Extractions = new List<DateColumnExtraction>
            {
                new()
                {
                    SourceColumn = "DateValue",
                    Components = new List<DateComponent> { DateComponent.Year },
                    OutputColumnTemplate = "Year_{column}"
                },
                new()
                {
                    SourceColumn = "DateValue",
                    Components = new List<DateComponent> { DateComponent.Month },
                    OutputColumnTemplate = "Month_{column}"
                }
            },
            AppendToSource = false
        };

        var task = new DateExtractionTask(_mockLogger.Object);
        var context = new TaskContext(options);

        // Act
        bool result = task.Execute(context);

        // Assert
        Assert.True(result);
        string[] lines = File.ReadAllLines(_testOutputPath);
        var headers = lines[0].Split(',');
        var firstRow = lines[1].Split(',');

        Assert.Contains("Year_DateValue", headers);
        Assert.Contains("Month_DateValue", headers);

        Assert.Equal("2024", firstRow[headers.ToList().IndexOf("Year_DateValue")]);
        Assert.Equal("1", firstRow[headers.ToList().IndexOf("Month_DateValue")]);
    }

    [Fact]
    public void Validate_WithNoExtractions_ShouldReturnError()
    {
        // Arrange
        var options = new DateExtractionOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Extractions = new List<DateColumnExtraction>()
        };

        // Act
        string[] errors = options.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("At least one date extraction must be specified"));
    }

    [Fact]
    public void Validate_WithEmptySourceColumn_ShouldReturnError()
    {
        // Arrange
        var options = new DateExtractionOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Extractions = new List<DateColumnExtraction>
            {
                new()
                {
                    SourceColumn = "",
                    Components = new List<DateComponent> { DateComponent.Year }
                }
            }
        };

        // Act
        string[] errors = options.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("Source column name cannot be empty"));
    }

    [Fact]
    public void Validate_WithNoComponents_ShouldReturnError()
    {
        // Arrange
        var options = new DateExtractionOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Extractions = new List<DateColumnExtraction>
            {
                new()
                {
                    SourceColumn = "DateValue",
                    Components = new List<DateComponent>()
                }
            }
        };

        // Act
        string[] errors = options.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("At least one component must be specified"));
    }

    [Fact]
    public void Validate_WithMissingOutputTemplate_ShouldReturnError()
    {
        // Arrange
        var options = new DateExtractionOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Extractions = new List<DateColumnExtraction>
            {
                new()
                {
                    SourceColumn = "DateValue",
                    Components = new List<DateComponent> { DateComponent.Year },
                    OutputColumnTemplate = null
                }
            },
            AppendToSource = false
        };

        // Act
        string[] errors = options.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("Output column template must be specified"));
    }
}