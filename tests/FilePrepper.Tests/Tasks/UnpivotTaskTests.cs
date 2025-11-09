using FilePrepper.Tasks;
using FilePrepper.Tasks.Unpivot;
using Microsoft.Extensions.Logging;
using Moq;

namespace FilePrepper.Tests.Tasks;

public class UnpivotTaskTests
{
    private readonly Mock<ILogger<UnpivotTask>> _mockLogger;
    private readonly UnpivotTask _task;

    public UnpivotTaskTests()
    {
        _mockLogger = new Mock<ILogger<UnpivotTask>>();
        _task = new UnpivotTask(_mockLogger.Object);
    }

    [Fact]
    public async Task UnpivotTask_Dataset006Example_ConvertsWideToLong()
    {
        // Arrange - Create test data mimicking Dataset 006 structure
        var tempInputFile = Path.GetTempFileName();
        var tempOutputFile = Path.GetTempFileName();

        try
        {
            // Create sample wide-format CSV
            var wideFormatData = """
작업지시번호,도면,품명,시작,종료,생산량(Kg),단가,금액,1차 출고날짜,1차 출고량,2차 출고날짜,2차 출고량,3차 출고날짜,3차 출고량
W001,D001,제품A,2024-01-01,2024-01-10,1000,5000,5000000,2024-01-15,300,2024-02-20,400,2024-03-25,300
W002,D002,제품B,2024-01-05,2024-01-12,500,3000,1500000,2024-01-18,200,2024-02-25,300,,,
""";
            await File.WriteAllTextAsync(tempInputFile, wideFormatData);

            var option = new UnpivotOption
            {
                InputPath = tempInputFile,
                OutputPath = tempOutputFile,
                BaseColumns = ["작업지시번호", "도면", "품명", "시작", "종료", "생산량(Kg)", "단가", "금액"],
                ColumnGroups =
                [
                    new ColumnPairGroup { Columns = ["1차 출고날짜", "1차 출고량"], IndexValue = "1" },
                    new ColumnPairGroup { Columns = ["2차 출고날짜", "2차 출고량"], IndexValue = "2" },
                    new ColumnPairGroup { Columns = ["3차 출고날짜", "3차 출고량"], IndexValue = "3" }
                ],
                IndexColumnName = "출고차수",
                ValueColumnNames = ["출고날짜", "출고량"],
                SkipEmptyRows = true,
                HasHeader = true
            };

            var context = new TaskContext
            {
                Options = option,
                InputPath = option.InputPath,
                OutputPath = option.OutputPath
            };

            // Act
            var result = await _task.ExecuteAsync(context);

            // Assert
            Assert.True(result);
            Assert.True(File.Exists(tempOutputFile));

            var outputLines = await File.ReadAllLinesAsync(tempOutputFile);

            // Header + 5 data rows (W001 has 3 shipments, W002 has 2 shipments)
            Assert.Equal(6, outputLines.Length);

            // Verify header
            var expectedHeader = "작업지시번호,도면,품명,시작,종료,생산량(Kg),단가,금액,출고차수,출고날짜,출고량";
            Assert.Equal(expectedHeader, outputLines[0]);

            // Verify first unpivoted row (W001, 1차 출고)
            Assert.Contains("W001", outputLines[1]);
            Assert.Contains("1", outputLines[1]);  // 출고차수
            Assert.Contains("2024-01-15", outputLines[1]);  // 출고날짜
            Assert.Contains("300", outputLines[1]);  // 출고량

            // Verify second unpivoted row (W001, 2차 출고)
            Assert.Contains("W001", outputLines[2]);
            Assert.Contains("2", outputLines[2]);
            Assert.Contains("2024-02-20", outputLines[2]);
            Assert.Contains("400", outputLines[2]);

            // Verify W002 rows exist
            var w002Rows = outputLines.Where(l => l.Contains("W002")).ToList();
            Assert.Equal(2, w002Rows.Count); // Only 2 shipments (3rd is empty and skipped)
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempInputFile)) File.Delete(tempInputFile);
            if (File.Exists(tempOutputFile)) File.Delete(tempOutputFile);
        }
    }

    [Fact]
    public void UnpivotOption_Validate_RequiresColumnGroups()
    {
        // Arrange
        var option = new UnpivotOption
        {
            InputPath = "test.csv",
            OutputPath = "output.csv",
            ColumnGroups = [], // Empty
            ValueColumnNames = ["Value"]
        };

        // Act
        var errors = option.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("column group must be specified"));
    }

    [Fact]
    public void UnpivotOption_Validate_RequiresValueColumnNames()
    {
        // Arrange
        var option = new UnpivotOption
        {
            InputPath = "test.csv",
            OutputPath = "output.csv",
            ColumnGroups = [new ColumnPairGroup { Columns = ["Col1", "Col2"] }],
            ValueColumnNames = [] // Empty
        };

        // Act
        var errors = option.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("Value column names must be specified"));
    }

    [Fact]
    public void UnpivotOption_Validate_RequiresMatchingColumnCounts()
    {
        // Arrange
        var option = new UnpivotOption
        {
            InputPath = "test.csv",
            OutputPath = "output.csv",
            ColumnGroups =
            [
                new ColumnPairGroup { Columns = ["Col1", "Col2"] },
                new ColumnPairGroup { Columns = ["Col3"] } // Different count
            ],
            ValueColumnNames = ["Value1", "Value2"]
        };

        // Act
        var errors = option.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("same number of columns"));
    }

    [Fact]
    public void UnpivotOption_Validate_RequiresMatchingValueColumnCount()
    {
        // Arrange
        var option = new UnpivotOption
        {
            InputPath = "test.csv",
            OutputPath = "output.csv",
            ColumnGroups =
            [
                new ColumnPairGroup { Columns = ["Col1", "Col2"] }
            ],
            ValueColumnNames = ["Value1"] // Should be 2 to match columns
        };

        // Act
        var errors = option.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("must match number of columns in each group"));
    }
}
