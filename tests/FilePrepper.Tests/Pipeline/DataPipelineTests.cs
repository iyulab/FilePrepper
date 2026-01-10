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
        var testDir = Path.Combine("TestData", $"FromCsv_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);
        var csvPath = Path.Combine(testDir, "simple.csv");
        await File.WriteAllTextAsync(csvPath, "Name,Age,Score\nAlice,25,85\nBob,30,90");

        // Act
        var pipeline = await DataPipeline.FromCsvAsync(csvPath);

        // Assert
        pipeline.Should().NotBeNull();
        pipeline.RowCount.Should().Be(2);
        pipeline.ColumnNames.Should().BeEquivalentTo(new[] { "Name", "Age", "Score" });
    }

    [Fact]
    public Task FromData_ShouldCreatePipelineFromInMemoryData()
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

        return Task.CompletedTask;
    }

    [Fact]
    public Task ChainedOperations_ShouldNotPerformFileIO()
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

        return Task.CompletedTask;
    }

    [Fact]
    public async Task ToCsv_ShouldOnlyWriteOnce_AfterAllTransformations()
    {
        // Arrange
        var testDir = Path.Combine("TestData", $"ToCsv_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);
        var outputPath = Path.Combine(testDir, "output.csv");
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

    #region ConcatCsvAsync Tests

    [Fact]
    public async Task ConcatCsvAsync_BasicConcat_Success()
    {
        // Arrange - Create 3 CSV files with same schema
        var testDir = Path.Combine("TestData", $"Concat_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);

        await File.WriteAllTextAsync(
            Path.Combine(testDir, "test-1.csv"),
            "Name,Age,Score\nAlice,25,85\nBob,30,90");

        await File.WriteAllTextAsync(
            Path.Combine(testDir, "test-2.csv"),
            "Name,Age,Score\nCharlie,35,95\nDavid,28,88\nEve,32,92");

        await File.WriteAllTextAsync(
            Path.Combine(testDir, "test-3.csv"),
            "Name,Age,Score\nFrank,40,87");

        // Act
        var result = await DataPipeline.ConcatCsvAsync("test-*.csv", testDir);

        // Assert
        result.RowCount.Should().Be(6); // 2 + 3 + 1
        result.ColumnNames.Should().BeEquivalentTo(new[] { "Name", "Age", "Score" });

        // Cleanup
        Directory.Delete(testDir, true);
    }

    [Fact]
    public async Task ConcatCsvAsync_WithSourceColumn_TracksFilenames()
    {
        // Arrange
        var testDir = Path.Combine("TestData", $"ConcatSource_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);

        await File.WriteAllTextAsync(
            Path.Combine(testDir, "file-1.csv"),
            "Value\n10");

        await File.WriteAllTextAsync(
            Path.Combine(testDir, "file-2.csv"),
            "Value\n20");

        // Act
        var result = await DataPipeline.ConcatCsvAsync(
            "file-*.csv",
            testDir,
            addSourceColumn: true);

        var df = result.ToDataFrame();

        // Assert
        df.ColumnNames.Should().Contain("SourceFile");
        df.Rows[0]["SourceFile"].Should().Be("file-1.csv");
        df.Rows[1]["SourceFile"].Should().Be("file-2.csv");

        // Cleanup
        Directory.Delete(testDir, true);
    }

    [Fact]
    public async Task ConcatCsvAsync_MismatchedHeaders_ThrowsException()
    {
        // Arrange - Create files with different headers
        var testDir = Path.Combine("TestData", $"ConcatMismatch_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);

        await File.WriteAllTextAsync(
            Path.Combine(testDir, "mismatch-1.csv"),
            "A,B,C\n1,2,3");

        await File.WriteAllTextAsync(
            Path.Combine(testDir, "mismatch-2.csv"),
            "A,B,D\n4,5,6");  // Different header: D instead of C

        // Act & Assert
        var act = async () => await DataPipeline.ConcatCsvAsync("mismatch-*.csv", testDir);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Header mismatch*mismatch-2.csv*");

        // Cleanup
        Directory.Delete(testDir, true);
    }

    [Fact]
    public async Task ConcatCsvAsync_NoFilesMatched_ReturnsEmpty()
    {
        // Arrange
        var testDir = Path.Combine("TestData", $"ConcatEmpty_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);

        // Act
        var result = await DataPipeline.ConcatCsvAsync("nonexistent-*.csv", testDir);

        // Assert
        result.RowCount.Should().Be(0);

        // Cleanup
        Directory.Delete(testDir, true);
    }

    [Fact]
    public async Task ConcatCsvAsync_AlphabeticalOrder_ProcessesInOrder()
    {
        // Arrange
        var testDir = Path.Combine("TestData", $"ConcatOrder_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);

        await File.WriteAllTextAsync(
            Path.Combine(testDir, "file-3.csv"),
            "Value\n3");

        await File.WriteAllTextAsync(
            Path.Combine(testDir, "file-1.csv"),
            "Value\n1");

        await File.WriteAllTextAsync(
            Path.Combine(testDir, "file-2.csv"),
            "Value\n2");

        // Act
        var result = await DataPipeline.ConcatCsvAsync(
            "file-*.csv",
            testDir,
            addSourceColumn: true);

        var df = result.ToDataFrame();

        // Assert - Files should be processed in alphabetical order
        df.Rows[0]["SourceFile"].Should().Be("file-1.csv");
        df.Rows[1]["SourceFile"].Should().Be("file-2.csv");
        df.Rows[2]["SourceFile"].Should().Be("file-3.csv");

        // Cleanup
        Directory.Delete(testDir, true);
    }

    #endregion

    #region ParseKoreanTime Tests

    [Fact]
    public void ParseKoreanTime_AM_ParsesCorrectly()
    {
        // Arrange
        var data = new[]
        {
            new Dictionary<string, string> { ["Time"] = "오전 9:01:18" },
            new Dictionary<string, string> { ["Time"] = "오전 11:30:45" }
        };

        // Act
        var result = DataPipeline.FromData(data)
            .ParseKoreanTime("Time", "ParsedTime")
            .ToDataFrame();

        // Assert
        var dt1 = DateTime.Parse(result.Rows[0]["ParsedTime"]);
        dt1.Hour.Should().Be(9);
        dt1.Minute.Should().Be(1);
        dt1.Second.Should().Be(18);

        var dt2 = DateTime.Parse(result.Rows[1]["ParsedTime"]);
        dt2.Hour.Should().Be(11);
        dt2.Minute.Should().Be(30);
        dt2.Second.Should().Be(45);
    }

    [Fact]
    public void ParseKoreanTime_PM_Converts24Hour()
    {
        // Arrange
        var data = new[]
        {
            new Dictionary<string, string> { ["Time"] = "오후 2:15:30" },
            new Dictionary<string, string> { ["Time"] = "오후 11:59:59" }
        };

        // Act
        var result = DataPipeline.FromData(data)
            .ParseKoreanTime("Time", "ParsedTime")
            .ToDataFrame();

        // Assert
        var dt1 = DateTime.Parse(result.Rows[0]["ParsedTime"]);
        dt1.Hour.Should().Be(14);  // 2 PM → 14:00

        var dt2 = DateTime.Parse(result.Rows[1]["ParsedTime"]);
        dt2.Hour.Should().Be(23);  // 11 PM → 23:00
    }

    [Theory]
    [InlineData("오전 12:00:00", 0)]   // Midnight
    [InlineData("오후 12:00:00", 12)]  // Noon
    [InlineData("오전 12:30:15", 0)]   // 12:30 AM
    [InlineData("오후 12:30:15", 12)]  // 12:30 PM
    public void ParseKoreanTime_EdgeCases_HandlesCorrectly(string timeStr, int expectedHour)
    {
        // Arrange
        var data = new[]
        {
            new Dictionary<string, string> { ["Time"] = timeStr }
        };

        // Act
        var result = DataPipeline.FromData(data)
            .ParseKoreanTime("Time", "Parsed")
            .ToDataFrame();

        // Assert
        var dt = DateTime.Parse(result.Rows[0]["Parsed"]);
        dt.Hour.Should().Be(expectedHour);
    }

    [Fact]
    public void ParseKoreanTime_InvalidFormat_HandlesGracefully()
    {
        // Arrange - Missing 오전/오후
        var data = new[]
        {
            new Dictionary<string, string> { ["Time"] = "9:01:18" },
            new Dictionary<string, string> { ["Time"] = "" }
        };

        // Act
        var result = DataPipeline.FromData(data)
            .ParseKoreanTime("Time", "ParsedTime")
            .ToDataFrame();

        // Assert - Should handle gracefully by leaving empty
        result.Rows[0]["ParsedTime"].Should().BeEmpty();
        result.Rows[1]["ParsedTime"].Should().BeEmpty();
    }

    [Fact]
    public void ParseKoreanTime_IntegrationWithExtractDateFeatures()
    {
        // Arrange - Real Dataset 010 scenario
        var data = new[]
        {
            new Dictionary<string, string>
            {
                ["Time"] = "오전 9:01:18",
                ["Temp"] = "50.2",
                ["Press"] = "1.2"
            },
            new Dictionary<string, string>
            {
                ["Time"] = "오후 2:15:30",
                ["Temp"] = "55.1",
                ["Press"] = "1.5"
            }
        };

        // Act - Full workflow: Parse Korean time → Extract features
        var result = DataPipeline.FromData(data)
            .ParseKoreanTime("Time", "ParsedTime")
            .ExtractDateFeatures("ParsedTime",
                DateFeatures.Hour | DateFeatures.Minute,
                removeOriginal: false)
            .ToDataFrame();

        // Assert
        result.ColumnNames.Should().Contain("ParsedTime_Hour");
        result.ColumnNames.Should().Contain("ParsedTime_Minute");

        result.Rows[0]["ParsedTime_Hour"].Should().Be("9");
        result.Rows[0]["ParsedTime_Minute"].Should().Be("1");

        result.Rows[1]["ParsedTime_Hour"].Should().Be("14");  // 2 PM
        result.Rows[1]["ParsedTime_Minute"].Should().Be("15");
    }

    #endregion

    #region Combined Usage Test (Dataset 010 Scenario)

    [Fact]
    public async Task Dataset010Scenario_ConcatAndParseKoreanTime()
    {
        // Arrange - Simulate Dataset 010 structure
        var testDir = Path.Combine("TestData", $"Dataset010_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);

        // Create 3 CSV files (simulating 33 kemp-*.csv files)
        await File.WriteAllTextAsync(
            Path.Combine(testDir, "kemp-1.csv"),
            "Time,Temp,Press,Vib,MotorAmp\n오전 9:01:18,50.2,1.2,0.1,2.3\n오전 9:01:28,50.3,1.2,0.1,2.3");

        await File.WriteAllTextAsync(
            Path.Combine(testDir, "kemp-2.csv"),
            "Time,Temp,Press,Vib,MotorAmp\n오전 9:01:38,50.4,1.2,0.1,2.3\n오후 2:15:30,55.1,1.5,0.2,2.8");

        await File.WriteAllTextAsync(
            Path.Combine(testDir, "kemp-3.csv"),
            "Time,Temp,Press,Vib,MotorAmp\n오후 2:15:40,55.2,1.5,0.2,2.8");

        // Act - Complete preprocessing workflow
        var pipeline = await DataPipeline.ConcatCsvAsync("kemp-*.csv", testDir);
        var result = pipeline
            .ParseKoreanTime("Time", "ParsedTime")
            .ExtractDateFeatures("ParsedTime",
                DateFeatures.Hour | DateFeatures.Minute)
            .ToDataFrame();

        // Assert
        result.Rows.Should().HaveCount(5);  // 2 + 2 + 1
        result.ColumnNames.Should().Contain("ParsedTime_Hour");
        result.ColumnNames.Should().Contain("ParsedTime_Minute");
        result.ColumnNames.Should().Contain("Temp");

        // Verify first row (오전 9:01:18)
        result.Rows[0]["ParsedTime_Hour"].Should().Be("9");
        result.Rows[0]["ParsedTime_Minute"].Should().Be("1");

        // Verify PM row (오후 2:15:30)
        result.Rows[3]["ParsedTime_Hour"].Should().Be("14");

        // Cleanup
        Directory.Delete(testDir, true);
    }

    #endregion

    #region Unpivot Tests

    [Fact]
    public void Unpivot_BasicTransformation_ConvertsWideToLong()
    {
        // Arrange - Wide format: Order, 1차_Date, 1차_Qty, 2차_Date, 2차_Qty
        var data = new[]
        {
            new Dictionary<string, string>
            {
                ["Order"] = "W001", ["Product"] = "A",
                ["1차_Date"] = "2024-01-15", ["1차_Qty"] = "300",
                ["2차_Date"] = "2024-02-20", ["2차_Qty"] = "400"
            },
            new Dictionary<string, string>
            {
                ["Order"] = "W002", ["Product"] = "B",
                ["1차_Date"] = "2024-01-18", ["1차_Qty"] = "200",
                ["2차_Date"] = "2024-02-25", ["2차_Qty"] = "300"
            }
        };

        // Act
        var result = DataPipeline.FromData(data)
            .Unpivot(
                baseColumns: new[] { "Order", "Product" },
                columnGroups: new[]
                {
                    new UnpivotColumnGroup { Columns = new[] { "1차_Date", "1차_Qty" }, IndexValue = "1" },
                    new UnpivotColumnGroup { Columns = new[] { "2차_Date", "2차_Qty" }, IndexValue = "2" }
                },
                indexColumn: "Shipment",
                valueColumns: new[] { "Date", "Qty" })
            .ToDataFrame();

        // Assert - Should have 4 rows (2 orders × 2 shipments)
        result.Rows.Should().HaveCount(4);
        result.ColumnNames.Should().BeEquivalentTo(new[] { "Order", "Product", "Shipment", "Date", "Qty" });

        // Verify first row (W001, 1차)
        result.Rows[0]["Order"].Should().Be("W001");
        result.Rows[0]["Shipment"].Should().Be("1");
        result.Rows[0]["Date"].Should().Be("2024-01-15");
        result.Rows[0]["Qty"].Should().Be("300");

        // Verify third row (W002, 1차)
        result.Rows[2]["Order"].Should().Be("W002");
        result.Rows[2]["Shipment"].Should().Be("1");
    }

    [Fact]
    public void Unpivot_SkipEmptyRows_OmitsRowsWithAllEmptyValues()
    {
        // Arrange - Third shipment has empty values
        var data = new[]
        {
            new Dictionary<string, string>
            {
                ["Order"] = "W001",
                ["1차_Qty"] = "300",
                ["2차_Qty"] = "400",
                ["3차_Qty"] = ""  // Empty - should be skipped
            }
        };

        // Act
        var result = DataPipeline.FromData(data)
            .Unpivot(
                baseColumns: new[] { "Order" },
                columnGroups: new[]
                {
                    new UnpivotColumnGroup { Columns = new[] { "1차_Qty" }, IndexValue = "1" },
                    new UnpivotColumnGroup { Columns = new[] { "2차_Qty" }, IndexValue = "2" },
                    new UnpivotColumnGroup { Columns = new[] { "3차_Qty" }, IndexValue = "3" }
                },
                indexColumn: "Index",
                valueColumns: new[] { "Qty" },
                skipEmptyRows: true)
            .ToDataFrame();

        // Assert - Should have 2 rows (empty 3차 skipped)
        result.Rows.Should().HaveCount(2);
        result.Rows.Select(r => r["Index"]).Should().BeEquivalentTo(new[] { "1", "2" });
    }

    [Fact]
    public void UnpivotSimple_SingleValueColumn_TransformsCorrectly()
    {
        // Arrange - Simple wide format with Q1, Q2, Q3, Q4 columns
        var data = new[]
        {
            new Dictionary<string, string>
            {
                ["Region"] = "North", ["Q1"] = "100", ["Q2"] = "150", ["Q3"] = "200", ["Q4"] = "250"
            },
            new Dictionary<string, string>
            {
                ["Region"] = "South", ["Q1"] = "80", ["Q2"] = "120", ["Q3"] = "160", ["Q4"] = "200"
            }
        };

        // Act
        var result = DataPipeline.FromData(data)
            .UnpivotSimple(
                baseColumns: new[] { "Region" },
                unpivotColumns: new[] { "Q1", "Q2", "Q3", "Q4" },
                indexColumn: "Quarter",
                valueColumn: "Sales")
            .ToDataFrame();

        // Assert - Should have 8 rows (2 regions × 4 quarters)
        result.Rows.Should().HaveCount(8);
        result.ColumnNames.Should().BeEquivalentTo(new[] { "Region", "Quarter", "Sales" });

        // Verify North Q1
        result.Rows[0]["Region"].Should().Be("North");
        result.Rows[0]["Quarter"].Should().Be("1");
        result.Rows[0]["Sales"].Should().Be("100");

        // Verify South Q4
        result.Rows[7]["Region"].Should().Be("South");
        result.Rows[7]["Quarter"].Should().Be("4");
        result.Rows[7]["Sales"].Should().Be("200");
    }

    [Fact]
    public void Unpivot_ValidationError_EmptyColumnGroups()
    {
        // Arrange
        var data = new[] { new Dictionary<string, string> { ["A"] = "1" } };
        var pipeline = DataPipeline.FromData(data);

        // Act & Assert
        var act = () => pipeline.Unpivot(
            new[] { "A" },
            Array.Empty<UnpivotColumnGroup>(),
            "Index",
            new[] { "Value" });

        act.Should().Throw<ArgumentException>()
            .WithMessage("*column group must be specified*");
    }

    [Fact]
    public void Unpivot_ValidationError_MismatchedColumnCounts()
    {
        // Arrange
        var data = new[] { new Dictionary<string, string> { ["A"] = "1", ["B"] = "2", ["C"] = "3" } };
        var pipeline = DataPipeline.FromData(data);

        // Act & Assert - Column groups have different counts
        var act = () => pipeline.Unpivot(
            new[] { "A" },
            new[]
            {
                new UnpivotColumnGroup { Columns = new[] { "B", "C" } },  // 2 columns
                new UnpivotColumnGroup { Columns = new[] { "B" } }       // 1 column - mismatch!
            },
            "Index",
            new[] { "Value1", "Value2" });

        act.Should().Throw<ArgumentException>()
            .WithMessage("*same number of columns*");
    }

    #endregion

    #region ConcatCsvAsync with Metadata Tests

    [Fact]
    public async Task ConcatCsvAsync_WithDatePreset_ExtractsDateFromFilename()
    {
        // Arrange - Create files with dates in filename
        var testDir = Path.Combine("TestData", $"ConcatMeta_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);

        await File.WriteAllTextAsync(
            Path.Combine(testDir, "data_2024-01-15.csv"),
            "Value\n10");

        await File.WriteAllTextAsync(
            Path.Combine(testDir, "data_2024-02-20.csv"),
            "Value\n20");

        // Act
        var result = await DataPipeline.ConcatCsvAsync(
            "data_*.csv",
            testDir,
            hasHeader: true,
            new FilenameMetadataOptions
            {
                Preset = FilenameMetadataPreset.DateOnly
            });

        var df = result.ToDataFrame();

        // Assert
        df.ColumnNames.Should().Contain("SourceFile");
        df.ColumnNames.Should().Contain("FileDate");

        df.Rows[0]["SourceFile"].Should().Be("data_2024-01-15.csv");
        df.Rows[0]["FileDate"].Should().Contain("2024-01-15");

        df.Rows[1]["SourceFile"].Should().Be("data_2024-02-20.csv");
        df.Rows[1]["FileDate"].Should().Contain("2024-02-20");

        // Cleanup
        Directory.Delete(testDir, true);
    }

    [Fact]
    public async Task ConcatCsvAsync_WithSensorDatePreset_ExtractsDateFromSensorFilename()
    {
        // Arrange - Sensor filename pattern: sensor-2021.09.06.csv
        var testDir = Path.Combine("TestData", $"ConcatSensor_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);

        await File.WriteAllTextAsync(
            Path.Combine(testDir, "sensor-2021.09.06.csv"),
            "Temp,Press\n50.2,1.2");

        await File.WriteAllTextAsync(
            Path.Combine(testDir, "sensor-2021.09.07.csv"),
            "Temp,Press\n51.3,1.3");

        // Act
        var result = await DataPipeline.ConcatCsvAsync(
            "sensor-*.csv",
            testDir,
            hasHeader: true,
            new FilenameMetadataOptions
            {
                Preset = FilenameMetadataPreset.SensorDate
            });

        var df = result.ToDataFrame();

        // Assert
        df.RowCount.Should().Be(2);
        df.ColumnNames.Should().Contain("FileDate");

        // Date should be extracted (format may vary)
        df.Rows[0]["FileDate"].Should().NotBeEmpty();
        df.Rows[1]["FileDate"].Should().NotBeEmpty();

        // Cleanup
        Directory.Delete(testDir, true);
    }

    [Fact]
    public async Task ConcatCsvAsync_WithCustomPattern_ExtractsCustomMetadata()
    {
        // Arrange - Custom pattern: region-north_2024-01.csv
        var testDir = Path.Combine("TestData", $"ConcatCustom_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);

        await File.WriteAllTextAsync(
            Path.Combine(testDir, "region-north_2024-01.csv"),
            "Sales\n100");

        await File.WriteAllTextAsync(
            Path.Combine(testDir, "region-south_2024-02.csv"),
            "Sales\n200");

        // Act
        var result = await DataPipeline.ConcatCsvAsync(
            "region-*.csv",
            testDir,
            hasHeader: true,
            new FilenameMetadataOptions
            {
                AddSourceColumn = true,
                CustomPatterns = new Dictionary<string, string>
                {
                    ["Region"] = @"region-(\w+)_",
                    ["Period"] = @"_(\d{4}-\d{2})"
                }
            });

        var df = result.ToDataFrame();

        // Assert
        df.ColumnNames.Should().Contain("Region");
        df.ColumnNames.Should().Contain("Period");

        df.Rows[0]["Region"].Should().Be("north");
        df.Rows[0]["Period"].Should().Be("2024-01");

        df.Rows[1]["Region"].Should().Be("south");
        df.Rows[1]["Period"].Should().Be("2024-02");

        // Cleanup
        Directory.Delete(testDir, true);
    }

    [Fact]
    public async Task ConcatCsvAsync_WithMetadata_DisableSourceColumn()
    {
        // Arrange
        var testDir = Path.Combine("TestData", $"ConcatNoSource_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);

        await File.WriteAllTextAsync(
            Path.Combine(testDir, "data_2024-01-15.csv"),
            "Value\n10");

        // Act
        var result = await DataPipeline.ConcatCsvAsync(
            "data_*.csv",
            testDir,
            hasHeader: true,
            new FilenameMetadataOptions
            {
                AddSourceColumn = false,
                Preset = FilenameMetadataPreset.DateOnly
            });

        var df = result.ToDataFrame();

        // Assert - Should have FileDate but NOT SourceFile
        df.ColumnNames.Should().NotContain("SourceFile");
        df.ColumnNames.Should().Contain("FileDate");

        // Cleanup
        Directory.Delete(testDir, true);
    }

    [Fact]
    public async Task ConcatCsvAsync_ManufacturingPreset_ExtractsMultipleFields()
    {
        // Arrange - Manufacturing pattern: batch_001_normal.csv
        var testDir = Path.Combine("TestData", $"ConcatMfg_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);

        await File.WriteAllTextAsync(
            Path.Combine(testDir, "batch_001_normal.csv"),
            "Value\n100");

        await File.WriteAllTextAsync(
            Path.Combine(testDir, "batch_002_outlier.csv"),
            "Value\n200");

        // Act
        var result = await DataPipeline.ConcatCsvAsync(
            "batch_*.csv",
            testDir,
            hasHeader: true,
            new FilenameMetadataOptions
            {
                Preset = FilenameMetadataPreset.Manufacturing
            });

        var df = result.ToDataFrame();

        // Assert
        df.ColumnNames.Should().Contain("BatchId");
        df.ColumnNames.Should().Contain("Category");

        df.Rows[0]["BatchId"].Should().Be("001");
        df.Rows[0]["Category"].Should().Be("normal");

        df.Rows[1]["BatchId"].Should().Be("002");
        df.Rows[1]["Category"].Should().Be("outlier");

        // Cleanup
        Directory.Delete(testDir, true);
    }

    #endregion
}
