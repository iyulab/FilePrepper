using FilePrepper.Pipeline;
using FilePrepper.Tasks.NormalizeData;
using FluentAssertions;
using Xunit;

namespace FilePrepper.Tests.Pipeline;

/// <summary>
/// SDK 프로그래밍 방식 사용 시나리오 통합 테스트
/// 목표: 실제 사용 사례를 통한 API 검증
/// </summary>
public class SdkUsageScenariosTests
{
    [Fact]
    public async Task Scenario_DataCleaning_WithoutMultipleFileIO()
    {
        // Given: 결측치와 이상치가 있는 데이터
        var data = new[]
        {
            new Dictionary<string, string> { ["Name"] = "Alice", ["Age"] = "25", ["Score"] = "" },
            new Dictionary<string, string> { ["Name"] = "Bob", ["Age"] = "", ["Score"] = "90" },
            new Dictionary<string, string> { ["Name"] = "Charlie", ["Age"] = "35", ["Score"] = "75" }
        };

        // When: Pipeline을 통한 연속 전처리 (파일 I/O 없음)
        var result = DataPipeline.FromData(data)
            .FillMissing(columns: new[] { "Age", "Score" }, method: FillMethod.Mean)
            .ToDataFrame();

        // Then: 모든 결측치가 채워짐
        result.Rows.Should().HaveCount(3);
        result.Rows[0]["Score"].Should().NotBeEmpty();
        result.Rows[1]["Age"].Should().NotBeEmpty();
    }

    [Fact]
    public async Task Scenario_MachineLearning_FeatureEngineering()
    {
        // Given: ML 학습을 위한 원시 데이터
        var trainingData = new[]
        {
            new Dictionary<string, string> { ["Price"] = "100", ["Quantity"] = "2", ["Discount"] = "0.1" },
            new Dictionary<string, string> { ["Price"] = "200", ["Quantity"] = "1", ["Discount"] = "0.2" }
        };

        // When: 특성 엔지니어링 파이프라인
        var result = DataPipeline.FromData(trainingData)
            // 1. 파생 변수 생성
            .AddColumn("Revenue", row =>
                (double.Parse(row["Price"]) * double.Parse(row["Quantity"]) *
                 (1 - double.Parse(row["Discount"]))).ToString())
            // 2. 정규화
            .Normalize(columns: new[] { "Price", "Quantity", "Revenue" },
                      method: NormalizationMethod.MinMax)
            // 3. 불필요한 컬럼 제거
            .RemoveColumns("Discount")
            .ToDataFrame();

        // Then: 전처리된 학습 데이터 준비 완료
        result.ColumnNames.Should().Contain("Revenue");
        result.ColumnNames.Should().NotContain("Discount");
        double.Parse(result.Rows[0]["Price"]).Should().BeInRange(0, 1); // 정규화 확인
    }

    [Fact]
    public async Task Scenario_ETL_Pipeline_CSVtoDatabaseReady()
    {
        // Given: CSV 파일 생성
        var csvPath = Path.Combine("TestData", "etl_source.csv");
        Directory.CreateDirectory("TestData");
        await File.WriteAllTextAsync(csvPath,
            "CustomerID,Name,Age,PurchaseAmount\n" +
            "1,Alice,25,100.50\n" +
            "2,Bob,30,200.75\n" +
            "3,Charlie,35,150.00");

        // When: ETL 파이프라인 (Extract → Transform → Load ready)
        var pipeline = await DataPipeline.FromCsvAsync(csvPath);
        var result = pipeline
            // Transform
            .AddColumn("AgeGroup", row =>
            {
                var age = int.Parse(row["Age"]);
                return age < 30 ? "Young" : "Adult";
            })
            .AddColumn("HighValue", row =>
            {
                var amount = double.Parse(row["PurchaseAmount"]);
                return amount > 150 ? "Yes" : "No";
            })
            .RenameColumn("PurchaseAmount", "Amount")
            .ToDataFrame();

        // Then: 데이터베이스 삽입 준비 완료
        result.RowCount.Should().Be(3);
        result.ColumnNames.Should().Contain("AgeGroup");
        result.ColumnNames.Should().Contain("HighValue");
        result.ColumnNames.Should().Contain("Amount");
        result.ColumnNames.Should().NotContain("PurchaseAmount");
    }

    [Fact]
    public async Task Scenario_DataAnalysis_FilterAndAggregate()
    {
        // Given: 분석할 판매 데이터
        var salesData = new[]
        {
            new Dictionary<string, string> { ["Region"] = "North", ["Revenue"] = "1000", ["Cost"] = "600" },
            new Dictionary<string, string> { ["Region"] = "South", ["Revenue"] = "800", ["Cost"] = "500" },
            new Dictionary<string, string> { ["Region"] = "North", ["Revenue"] = "1200", ["Cost"] = "700" },
            new Dictionary<string, string> { ["Region"] = "East", ["Revenue"] = "500", ["Cost"] = "400" }
        };

        // When: 고수익 지역만 필터링하고 이익률 계산
        var result = DataPipeline.FromData(salesData)
            .AddColumn("Profit", row =>
                (double.Parse(row["Revenue"]) - double.Parse(row["Cost"])).ToString())
            .AddColumn("ProfitMargin", row =>
            {
                var revenue = double.Parse(row["Revenue"]);
                var profit = double.Parse(row["Profit"]);
                return ((profit / revenue) * 100).ToString("F2");
            })
            .FilterRows(row => double.Parse(row["Profit"]) > 400)
            .ToDataFrame();

        // Then: 고수익 레코드만 추출됨 (Profit > 400: North 1000-600=400 제외, North 1200-700=500 포함)
        result.Rows.Should().HaveCount(1); // Only 1 region with profit > 400
        result.Rows.All(r => double.Parse(r["Profit"]) > 400).Should().BeTrue();
    }

    [Fact]
    public async Task Scenario_NoFileIO_PureInMemoryProcessing()
    {
        // Given: 메모리 상의 데이터 (파일 없음)
        var inMemoryData = Enumerable.Range(1, 100).Select(i => new Dictionary<string, string>
        {
            ["ID"] = i.ToString(),
            ["Value"] = (i * 10).ToString(),
            ["Category"] = i % 2 == 0 ? "Even" : "Odd"
        });

        // When: 파일 I/O 없이 순수 메모리 처리
        var result = DataPipeline.FromData(inMemoryData)
            .FilterRows(row => row["Category"] == "Even")
            .Normalize(columns: new[] { "Value" }, method: NormalizationMethod.ZScore)
            .AddColumn("Processed", _ => "✓")
            .ToDataFrame();

        // Then: 파일 생성 없이 결과 획득
        result.RowCount.Should().Be(50); // Even numbers only
        result.ColumnNames.Should().Contain("Processed");
    }

    [Fact]
    public async Task Scenario_ChainedOperations_PreserveDataIntegrity()
    {
        // Given: 복잡한 변환 체인
        var data = new[]
        {
            new Dictionary<string, string> { ["A"] = "10", ["B"] = "20", ["C"] = "temp" },
            new Dictionary<string, string> { ["A"] = "15", ["B"] = "25", ["C"] = "temp" }
        };

        // When: 10단계 변환 체인
        var result = DataPipeline.FromData(data)
            .RemoveColumns("C")                                    // 1
            .AddColumn("Sum", r => (int.Parse(r["A"]) + int.Parse(r["B"])).ToString()) // 2
            .AddColumn("Avg", r => ((int.Parse(r["A"]) + int.Parse(r["B"])) / 2.0).ToString()) // 3
            .RenameColumn("A", "ValueA")                           // 4
            .RenameColumn("B", "ValueB")                           // 5
            .Normalize(new[] { "Sum", "Avg" }, NormalizationMethod.MinMax) // 6
            .AddColumn("Flag", _ => "processed")                   // 7
            .FilterRows(r => r["Flag"] == "processed")             // 8 (no-op filter)
            .ToDataFrame();

        // Then: 데이터 무결성 유지
        result.RowCount.Should().Be(2);
        result.ColumnNames.Should().Contain("ValueA");
        result.ColumnNames.Should().NotContain("A");
        result.ColumnNames.Should().Contain("Flag");
    }

    [Fact]
    public async Task Scenario_SaveOnlyAtEnd_MinimizeFileIO()
    {
        // Given: 여러 변환 단계
        var data = new[]
        {
            new Dictionary<string, string> { ["X"] = "1", ["Y"] = "2" },
            new Dictionary<string, string> { ["X"] = "3", ["Y"] = "4" }
        };

        var outputPath = Path.Combine("TestData", "final_output.csv");
        Directory.CreateDirectory("TestData");

        // When: 중간 파일 생성 없이 최종 결과만 저장
        await DataPipeline.FromData(data)
            .AddColumn("Sum", r => (int.Parse(r["X"]) + int.Parse(r["Y"])).ToString())
            .AddColumn("Product", r => (int.Parse(r["X"]) * int.Parse(r["Y"])).ToString())
            .Normalize(new[] { "Sum", "Product" }, NormalizationMethod.MinMax)
            .ToCsvAsync(outputPath);  // 파일 I/O는 여기서만 발생

        // Then: 파일이 한 번만 생성됨
        File.Exists(outputPath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(outputPath);
        content.Should().Contain("Sum");
        content.Should().Contain("Product");
    }
}
