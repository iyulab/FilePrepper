using FilePrepper.Pipeline;
using FluentAssertions;
using System.Text.Json;
using System.Xml.Linq;

namespace FilePrepper.Tests.Pipeline;

public class MultiFormatPipelineTests : IDisposable
{
    private readonly string _testDataDir = "TestData";

    public MultiFormatPipelineTests()
    {
        Directory.CreateDirectory(_testDataDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDataDir))
        {
            Directory.Delete(_testDataDir, true);
        }
    }

    [Fact]
    public async Task Should_Read_And_Write_Excel_File()
    {
        // Arrange
        var excelPath = Path.Combine(_testDataDir, "test.xlsx");
        var outputPath = Path.Combine(_testDataDir, "output.xlsx");

        // Create test Excel file using EPPlus
        var testData = new List<Dictionary<string, string>>
        {
            new() { ["Name"] = "Alice", ["Age"] = "25", ["City"] = "Seoul" },
            new() { ["Name"] = "Bob", ["Age"] = "30", ["City"] = "Busan" }
        };
        var headers = new List<string> { "Name", "Age", "City" };

        await Utils.ExcelUtils.WriteExcelFileAsync(excelPath, testData, headers);

        // Act
        var pipeline = await DataPipeline.FromExcelAsync(excelPath);
        await pipeline.ToExcelAsync(outputPath);

        // Assert
        File.Exists(outputPath).Should().BeTrue();
        var (records, _) = await Utils.ExcelUtils.ReadExcelFileAsync(outputPath);
        records.Should().HaveCount(2);
        records[0]["Name"].Should().Be("Alice");
        records[1]["Age"].Should().Be("30");
    }

    [Fact]
    public async Task Should_Transform_Excel_Data_And_Save_To_CSV()
    {
        // Arrange
        var excelPath = Path.Combine(_testDataDir, "input.xlsx");
        var csvPath = Path.Combine(_testDataDir, "output.csv");

        var testData = new List<Dictionary<string, string>>
        {
            new() { ["Name"] = "Alice", ["Score"] = "85" },
            new() { ["Name"] = "Bob", ["Score"] = "92" }
        };
        var headers = new List<string> { "Name", "Score" };
        await Utils.ExcelUtils.WriteExcelFileAsync(excelPath, testData, headers);

        // Act
        var pipeline = await DataPipeline.FromExcelAsync(excelPath);
        await pipeline
            .AddColumn("Grade", row =>
            {
                var score = int.Parse(row["Score"]);
                return score >= 90 ? "A" : "B";
            })
            .ToCsvAsync(csvPath);

        // Assert
        var lines = await File.ReadAllLinesAsync(csvPath);
        lines.Should().Contain(l => l.Contains("Grade"));
        lines.Should().Contain(l => l.Contains("A")); // Bob's grade
    }

    [Fact]
    public async Task Should_Read_And_Write_JSON_File()
    {
        // Arrange
        var jsonPath = Path.Combine(_testDataDir, "test.json");
        var outputPath = Path.Combine(_testDataDir, "output.json");

        var testData = new List<Dictionary<string, object>>
        {
            new() { ["Name"] = "Alice", ["Age"] = 25, ["Active"] = true },
            new() { ["Name"] = "Bob", ["Age"] = 30, ["Active"] = false }
        };

        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(testData));

        // Act
        var pipeline = await DataPipeline.FromJsonAsync(jsonPath);
        await pipeline.ToJsonAsync(outputPath);

        // Assert
        File.Exists(outputPath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(outputPath);
        content.Should().Contain("Alice");
        content.Should().Contain("Bob");
    }

    [Fact]
    public async Task Should_Transform_JSON_To_Excel()
    {
        // Arrange
        var jsonPath = Path.Combine(_testDataDir, "data.json");
        var excelPath = Path.Combine(_testDataDir, "output.xlsx");

        var testData = new List<Dictionary<string, object>>
        {
            new() { ["Product"] = "Apple", ["Price"] = "1.5", ["Stock"] = "100" },
            new() { ["Product"] = "Banana", ["Price"] = "0.8", ["Stock"] = "150" }
        };

        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(testData));

        // Act
        var pipeline = await DataPipeline.FromJsonAsync(jsonPath);
        await pipeline
            .FilterRows(row => double.Parse(row["Price"]) > 1.0)
            .ToExcelAsync(excelPath);

        // Assert
        var (records, _) = await Utils.ExcelUtils.ReadExcelFileAsync(excelPath);
        records.Should().HaveCount(1);
        records[0]["Product"].Should().Be("Apple");
    }

    [Fact]
    public async Task Should_Read_And_Write_XML_File()
    {
        // Arrange
        var xmlPath = Path.Combine(_testDataDir, "test.xml");
        var outputPath = Path.Combine(_testDataDir, "output.xml");

        var xml = new XDocument(
            new XElement("data",
                new XElement("row",
                    new XElement("Name", "Alice"),
                    new XElement("Age", "25"),
                    new XElement("Department", "Engineering")),
                new XElement("row",
                    new XElement("Name", "Bob"),
                    new XElement("Age", "30"),
                    new XElement("Department", "Sales"))
            )
        );

        await File.WriteAllTextAsync(xmlPath, xml.ToString());

        // Act
        var pipeline = await DataPipeline.FromXmlAsync(xmlPath);
        await pipeline.ToXmlAsync(outputPath);

        // Assert
        File.Exists(outputPath).Should().BeTrue();
        var outputXml = XDocument.Load(outputPath);
        outputXml.Descendants("row").Should().HaveCount(2);
        outputXml.Descendants("Name").First().Value.Should().Be("Alice");
    }

    [Fact]
    public async Task Should_Convert_CSV_To_All_Formats()
    {
        // Arrange
        var csvPath = Path.Combine(_testDataDir, "source.csv");
        var csvContent = "Name,Age,Salary\nAlice,25,50000\nBob,30,60000";
        await File.WriteAllTextAsync(csvPath, csvContent);

        var excelPath = Path.Combine(_testDataDir, "output.xlsx");
        var jsonPath = Path.Combine(_testDataDir, "output.json");
        var xmlPath = Path.Combine(_testDataDir, "output.xml");

        // Act - Create pipeline once, output to multiple formats
        var pipeline = await DataPipeline.FromCsvAsync(csvPath);

        await pipeline.ToExcelAsync(excelPath);
        await pipeline.ToJsonAsync(jsonPath);
        await pipeline.ToXmlAsync(xmlPath);

        // Assert
        File.Exists(excelPath).Should().BeTrue();
        File.Exists(jsonPath).Should().BeTrue();
        File.Exists(xmlPath).Should().BeTrue();

        // Verify Excel
        var (excelRecords, _) = await Utils.ExcelUtils.ReadExcelFileAsync(excelPath);
        excelRecords.Should().HaveCount(2);

        // Verify JSON
        var jsonContent = await File.ReadAllTextAsync(jsonPath);
        jsonContent.Should().Contain("Alice");

        // Verify XML
        var xmlDoc = XDocument.Load(xmlPath);
        xmlDoc.Descendants("row").Should().HaveCount(2);
    }

    [Fact]
    public async Task Should_Handle_Excel_With_Custom_Sheet_Name()
    {
        // Arrange
        var excelPath = Path.Combine(_testDataDir, "custom.xlsx");
        var testData = new List<Dictionary<string, string>>
        {
            new() { ["ID"] = "1", ["Value"] = "Test" }
        };
        var headers = new List<string> { "ID", "Value" };

        // Act
        await Utils.ExcelUtils.WriteExcelFileAsync(excelPath, testData, headers, sheetName: "CustomSheet");
        var pipeline = await DataPipeline.FromExcelAsync(excelPath, sheetName: "CustomSheet");

        // Assert
        pipeline.RowCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_Handle_XML_With_Custom_Row_Element()
    {
        // Arrange
        var xmlPath = Path.Combine(_testDataDir, "custom.xml");
        var xml = new XDocument(
            new XElement("database",
                new XElement("record",
                    new XElement("ID", "1"),
                    new XElement("Name", "Alice")),
                new XElement("record",
                    new XElement("ID", "2"),
                    new XElement("Name", "Bob"))
            )
        );
        await File.WriteAllTextAsync(xmlPath, xml.ToString());

        // Act
        var pipeline = await DataPipeline.FromXmlAsync(xmlPath, rowElement: "record");

        // Assert
        pipeline.RowCount.Should().Be(2);
        var df = pipeline.ToDataFrame();
        df.ColumnNames.Should().Contain("ID");
        df.ColumnNames.Should().Contain("Name");
    }

    [Fact]
    public async Task Should_Chain_Transformations_Across_Formats()
    {
        // Arrange: Excel → Transform → JSON
        var excelPath = Path.Combine(_testDataDir, "sales.xlsx");
        var jsonPath = Path.Combine(_testDataDir, "processed.json");

        var salesData = new List<Dictionary<string, string>>
        {
            new() { ["Product"] = "Laptop", ["Price"] = "1000", ["Quantity"] = "5" },
            new() { ["Product"] = "Mouse", ["Price"] = "20", ["Quantity"] = "50" },
            new() { ["Product"] = "Keyboard", ["Price"] = "80", ["Quantity"] = "30" }
        };
        await Utils.ExcelUtils.WriteExcelFileAsync(excelPath, salesData, new List<string> { "Product", "Price", "Quantity" });

        // Act
        var pipeline = await DataPipeline.FromExcelAsync(excelPath);
        await pipeline
            .AddColumn("Total", row =>
                (double.Parse(row["Price"]) * double.Parse(row["Quantity"])).ToString())
            .FilterRows(row => double.Parse(row["Total"]) >= 1000)
            .ToJsonAsync(jsonPath);

        // Assert
        var jsonContent = await File.ReadAllTextAsync(jsonPath);
        jsonContent.Should().Contain("Laptop");   // Total = 5000
        jsonContent.Should().Contain("Mouse");    // Total = 1000
        jsonContent.Should().Contain("Keyboard"); // Total = 2400
        // All items have Total >= 1000
    }
}
