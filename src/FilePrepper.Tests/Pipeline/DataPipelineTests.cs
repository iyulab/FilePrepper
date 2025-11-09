using FilePrepper.Pipeline;
using FluentAssertions;
using Xunit;

namespace FilePrepper.Tests.Pipeline;

/// <summary>
/// TDD: DataPipeline 기본 동작 테스트
/// 목표: 파일 I/O 최소화, 연속적 전처리, Fluent API
/// </summary>
public class DataPipelineTests
{
    [Fact]
    public async Task FromCsv_ShouldLoadDataIntoMemory()
    {
        // Arrange
        var csvPath = Path.Combine("TestData", "simple.csv");
        Directory.CreateDirectory("TestData");
        await File.WriteAllTextAsync(csvPath, "Name,Age,Score\nAlice,25,85\nBob,30,90");

        // Act
        var pipeline = await DataPipeline.FromCsvAsync(csvPath);

        // Assert
        pipeline.Should().NotBeNull();
        pipeline.RowCount.Should().Be(2);
        pipeline.ColumnNames.Should().BeEquivalentTo(new[] { "Name", "Age", "Score" });
    }

    [Fact]
    public async Task FromData_ShouldCreatePipelineFromInMemoryData()
    {
        // Arrange
        var data = new[]
        {
            new Dictionary<string, string> { ["Name"] = "Alice", ["Age"] = "25" },
            new Dictionary<string, string> { ["Name"] = "Bob", ["Age"] = "30" }
        };

        // Act
        var pipeline = DataPipeline.FromData(data);

        // Assert
        pipeline.RowCount.Should().Be(2);
        pipeline.ColumnNames.Should().BeEquivalentTo(new[] { "Name", "Age" });
    }

    [Fact]
    public async Task ChainedOperations_ShouldNotPerformFileIO()
    {
        // Arrange
        var data = new[]
        {
            new Dictionary<string, string> { ["Name"] = "Alice", ["Score"] = "85" },
            new Dictionary<string, string> { ["Name"] = "Bob", ["Score"] = "90" }
        };
        var pipeline = DataPipeline.FromData(data);

        // Act - 연속적인 전처리 (파일 I/O 없이 in-memory로 처리)
        var result = pipeline
            .AddColumn("HighScore", row => int.Parse(row["Score"]) > 87 ? "Yes" : "No")
            .FilterRows(row => row["Name"].StartsWith("A"))
            .ToDataFrame();

        // Assert
        result.Rows.Should().HaveCount(1);
        result.Rows[0]["Name"].Should().Be("Alice");
        result.Rows[0]["HighScore"].Should().Be("No");
    }

    [Fact]
    public async Task ToCsv_ShouldOnlyWriteOnce_AfterAllTransformations()
    {
        // Arrange
        Directory.CreateDirectory("TestData");
        var outputPath = Path.Combine("TestData", "output.csv");
        var data = new[]
        {
            new Dictionary<string, string> { ["Value"] = "10" },
            new Dictionary<string, string> { ["Value"] = "20" }
        };

        // Act
        await DataPipeline.FromData(data)
            .AddColumn("Double", row => (int.Parse(row["Value"]) * 2).ToString())
            .ToCsvAsync(outputPath);

        // Assert
        File.Exists(outputPath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(outputPath);
        content.Should().Contain("Value,Double");
        content.Should().Contain("10,20");
    }

    [Fact]
    public void GetColumn_ShouldReturnColumnValues()
    {
        // Arrange
        var data = new[]
        {
            new Dictionary<string, string> { ["Name"] = "Alice", ["Age"] = "25" },
            new Dictionary<string, string> { ["Name"] = "Bob", ["Age"] = "30" }
        };
        var pipeline = DataPipeline.FromData(data);

        // Act
        var ages = pipeline.GetColumn("Age");

        // Assert
        ages.Should().BeEquivalentTo(new[] { "25", "30" });
    }

    [Fact]
    public void ToDataFrame_ShouldReturnImmutableSnapshot()
    {
        // Arrange
        var data = new[]
        {
            new Dictionary<string, string> { ["Name"] = "Alice" }
        };
        var pipeline = DataPipeline.FromData(data);

        // Act
        var df1 = pipeline.ToDataFrame();
        var df2 = pipeline.AddColumn("NewCol", _ => "Value").ToDataFrame();

        // Assert
        df1.ColumnNames.Should().NotContain("NewCol");
        df2.ColumnNames.Should().Contain("NewCol");
    }

    [Fact]
    public void ParseDateTime_ShouldConvertStringToDateTime()
    {
        // Arrange
        var data = new[]
        {
            new Dictionary<string, string> { ["Date"] = "2022-08-10 07:57" },
            new Dictionary<string, string> { ["Date"] = "2022-08-15 14:30" }
        };
        var pipeline = DataPipeline.FromData(data);

        // Act
        var result = pipeline
            .ParseDateTime("Date", "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss")
            .ToDataFrame();

        // Assert
        result.Rows[0]["Date"].Should().Be("2022-08-10 07:57:00");
        result.Rows[1]["Date"].Should().Be("2022-08-15 14:30:00");
    }

    [Fact]
    public void ParseExcelDate_ShouldConvertNumericDateToDateTime()
    {
        // Arrange - 44783 = 2022-08-10 in Excel
        var data = new[]
        {
            new Dictionary<string, string> { ["OrderDate"] = "44783" },
            new Dictionary<string, string> { ["OrderDate"] = "44804" } // 2022-08-31
        };
        var pipeline = DataPipeline.FromData(data);

        // Act
        var result = pipeline
            .ParseExcelDate("OrderDate", "yyyy-MM-dd")
            .ToDataFrame();

        // Assert
        result.Rows[0]["OrderDate"].Should().Be("2022-08-10");
        result.Rows[1]["OrderDate"].Should().Be("2022-08-31");
    }

    [Fact]
    public void ExtractDateFeatures_ShouldCreateDateComponents()
    {
        // Arrange
        var data = new[]
        {
            new Dictionary<string, string> { ["DateTime"] = "2022-08-10 14:30" }
        };
        var pipeline = DataPipeline.FromData(data);

        // Act
        var result = pipeline
            .ExtractDateFeatures("DateTime",
                DateFeatures.Year | DateFeatures.Month | DateFeatures.Day |
                DateFeatures.Hour | DateFeatures.DayOfWeek,
                removeOriginal: false)
            .ToDataFrame();

        // Assert
        result.ColumnNames.Should().Contain("DateTime_Year");
        result.ColumnNames.Should().Contain("DateTime_Month");
        result.ColumnNames.Should().Contain("DateTime_Day");
        result.ColumnNames.Should().Contain("DateTime_Hour");
        result.ColumnNames.Should().Contain("DateTime_DayOfWeek");

        result.Rows[0]["DateTime_Year"].Should().Be("2022");
        result.Rows[0]["DateTime_Month"].Should().Be("8");
        result.Rows[0]["DateTime_Day"].Should().Be("10");
        result.Rows[0]["DateTime_Hour"].Should().Be("14");
        result.Rows[0]["DateTime_DayOfWeek"].Should().Be("3"); // Wednesday
    }

    [Fact]
    public void ExtractDateFeatures_WithRemoveOriginal_ShouldRemoveDateTimeColumn()
    {
        // Arrange
        var data = new[]
        {
            new Dictionary<string, string> { ["DateTime"] = "2022-08-10 14:30" }
        };
        var pipeline = DataPipeline.FromData(data);

        // Act
        var result = pipeline
            .ExtractDateFeatures("DateTime", DateFeatures.Year | DateFeatures.Month, removeOriginal: true)
            .ToDataFrame();

        // Assert
        result.ColumnNames.Should().NotContain("DateTime");
        result.ColumnNames.Should().Contain("DateTime_Year");
        result.ColumnNames.Should().Contain("DateTime_Month");
    }

    [Fact]
    public void DateTimePipeline_FullWorkflow_ExcelToFeatures()
    {
        // Arrange - Dataset 001 시나리오 (Excel 날짜 → DateTime 파싱 → Feature 추출)
        var data = new[]
        {
            new Dictionary<string, string>
            {
                ["Order_date"] = "44783",  // Excel date
                ["Quantity"] = "100"
            },
            new Dictionary<string, string>
            {
                ["Order_date"] = "44804",
                ["Quantity"] = "150"
            }
        };

        // Act
        var result = DataPipeline.FromData(data)
            .ParseExcelDate("Order_date", "yyyy-MM-dd HH:mm:ss")  // Step 1: Parse Excel date
            .ExtractDateFeatures("Order_date",                     // Step 2: Extract features
                DateFeatures.Year | DateFeatures.Month | DateFeatures.DayOfWeek,
                removeOriginal: false)
            .ToDataFrame();

        // Assert
        result.Rows.Should().HaveCount(2);
        result.ColumnNames.Should().Contain("Order_date_Year");
        result.ColumnNames.Should().Contain("Order_date_Month");
        result.ColumnNames.Should().Contain("Order_date_DayOfWeek");

        result.Rows[0]["Order_date_Year"].Should().Be("2022");
        result.Rows[0]["Order_date_Month"].Should().Be("8");
        result.Rows[0]["Quantity"].Should().Be("100");
    }
}
