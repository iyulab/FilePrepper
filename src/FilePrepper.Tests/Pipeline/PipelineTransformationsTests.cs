using FilePrepper.Pipeline;
using FilePrepper.Tasks.NormalizeData;
using FluentAssertions;
using Xunit;
using FillMethod = FilePrepper.Pipeline.FillMethod;

namespace FilePrepper.Tests.Pipeline;

/// <summary>
/// TDD: Pipeline을 통한 데이터 변환 테스트
/// 목표: 기존 Task들을 Pipeline 방식으로 사용 가능
/// </summary>
public class PipelineTransformationsTests
{
    [Fact]
    public void Normalize_ShouldNormalizeNumericColumns()
    {
        // Arrange
        var data = new[]
        {
            new Dictionary<string, string> { ["Name"] = "Alice", ["Score"] = "50" },
            new Dictionary<string, string> { ["Name"] = "Bob", ["Score"] = "100" }
        };
        var pipeline = DataPipeline.FromData(data);

        // Act
        var result = pipeline
            .Normalize(columns: new[] { "Score" }, method: NormalizationMethod.MinMax)
            .ToDataFrame();

        // Assert
        double.Parse(result.Rows[0]["Score"]).Should().BeApproximately(0.0, 0.01);
        double.Parse(result.Rows[1]["Score"]).Should().BeApproximately(1.0, 0.01);
    }

    [Fact]
    public void FillMissing_ShouldFillMissingValues()
    {
        // Arrange
        var data = new[]
        {
            new Dictionary<string, string> { ["Value"] = "10" },
            new Dictionary<string, string> { ["Value"] = "" },
            new Dictionary<string, string> { ["Value"] = "30" }
        };
        var pipeline = DataPipeline.FromData(data);

        // Act
        var result = pipeline
            .FillMissing(columns: new[] { "Value" }, method: FillMethod.Mean)
            .ToDataFrame();

        // Assert
        result.Rows[1]["Value"].Should().Be("20"); // (10 + 30) / 2
    }

    [Fact]
    public void FilterRows_ShouldFilterByCondition()
    {
        // Arrange
        var data = new[]
        {
            new Dictionary<string, string> { ["Age"] = "25", ["Score"] = "85" },
            new Dictionary<string, string> { ["Age"] = "30", ["Score"] = "90" },
            new Dictionary<string, string> { ["Age"] = "35", ["Score"] = "75" }
        };
        var pipeline = DataPipeline.FromData(data);

        // Act
        var result = pipeline
            .FilterRows(row => int.Parse(row["Age"]) >= 30)
            .ToDataFrame();

        // Assert
        result.Rows.Should().HaveCount(2);
        result.Rows[0]["Age"].Should().Be("30");
        result.Rows[1]["Age"].Should().Be("35");
    }

    [Fact]
    public void AddColumn_ShouldAddComputedColumn()
    {
        // Arrange
        var data = new[]
        {
            new Dictionary<string, string> { ["Price"] = "100", ["Quantity"] = "2" },
            new Dictionary<string, string> { ["Price"] = "50", ["Quantity"] = "3" }
        };
        var pipeline = DataPipeline.FromData(data);

        // Act
        var result = pipeline
            .AddColumn("Total", row => (int.Parse(row["Price"]) * int.Parse(row["Quantity"])).ToString())
            .ToDataFrame();

        // Assert
        result.Rows[0]["Total"].Should().Be("200");
        result.Rows[1]["Total"].Should().Be("150");
    }

    [Fact]
    public void RemoveColumns_ShouldRemoveSpecifiedColumns()
    {
        // Arrange
        var data = new[]
        {
            new Dictionary<string, string> { ["Name"] = "Alice", ["TempCol"] = "X", ["Score"] = "85" }
        };
        var pipeline = DataPipeline.FromData(data);

        // Act
        var result = pipeline
            .RemoveColumns("TempCol")
            .ToDataFrame();

        // Assert
        result.ColumnNames.Should().BeEquivalentTo(new[] { "Name", "Score" });
    }

    [Fact]
    public void RenameColumn_ShouldRenameColumn()
    {
        // Arrange
        var data = new[]
        {
            new Dictionary<string, string> { ["OldName"] = "Value1" }
        };
        var pipeline = DataPipeline.FromData(data);

        // Act
        var result = pipeline
            .RenameColumn("OldName", "NewName")
            .ToDataFrame();

        // Assert
        result.ColumnNames.Should().Contain("NewName");
        result.ColumnNames.Should().NotContain("OldName");
        result.Rows[0]["NewName"].Should().Be("Value1");
    }

    [Fact]
    public void ChainMultipleTransformations_ShouldApplyInOrder()
    {
        // Arrange
        var data = new[]
        {
            new Dictionary<string, string> { ["Name"] = "Alice", ["Score"] = "50", ["Temp"] = "X" },
            new Dictionary<string, string> { ["Name"] = "Bob", ["Score"] = "100", ["Temp"] = "Y" }
        };
        var pipeline = DataPipeline.FromData(data);

        // Act
        var result = pipeline
            .RemoveColumns("Temp")
            .Normalize(columns: new[] { "Score" }, method: NormalizationMethod.MinMax)
            .AddColumn("Grade", row => double.Parse(row["Score"]) > 0.5 ? "A" : "B")
            .FilterRows(row => row["Grade"] == "A")
            .ToDataFrame();

        // Assert
        result.Rows.Should().HaveCount(1);
        result.Rows[0]["Name"].Should().Be("Bob");
        result.ColumnNames.Should().NotContain("Temp");
        result.ColumnNames.Should().Contain("Grade");
    }
}
