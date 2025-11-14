using FilePrepper.Pipeline;
using FilePrepper.Tasks.WindowOps;
using FluentAssertions;
using Xunit;

namespace FilePrepper.Tests.Pipeline;

/// <summary>
/// Comprehensive tests for GroupBy and Aggregate operations
/// Covers: basic aggregations, multiple aggregations, edge cases, error handling
/// </summary>
public class GroupByAggregateTests
{
    #region Basic Aggregation Tests

    [Fact]
    public void GroupBy_WithMeanAggregation_CalculatesCorrectly()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["batch_id"] = "A", ["temperature"] = "100.0" },
            new Dictionary<string, string> { ["batch_id"] = "A", ["temperature"] = "110.0" },
            new Dictionary<string, string> { ["batch_id"] = "B", ["temperature"] = "200.0" },
            new Dictionary<string, string> { ["batch_id"] = "B", ["temperature"] = "220.0" }
        });

        // Act
        var result = data
            .GroupBy("batch_id")
            .Aggregate(new[] { ("temperature", AggregationMethod.Mean) });

        // Assert
        var rows = result.ToDataFrame().Rows.ToList();
        rows.Should().HaveCount(2);
        rows.First(r => r["batch_id"] == "A")["temperature_mean"].Should().Be("105");
        rows.First(r => r["batch_id"] == "B")["temperature_mean"].Should().Be("210");
    }

    [Fact]
    public void GroupBy_WithMultipleAggregations_CreatesCorrectColumns()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["batch"] = "1", ["temp"] = "100" },
            new Dictionary<string, string> { ["batch"] = "1", ["temp"] = "120" },
            new Dictionary<string, string> { ["batch"] = "2", ["temp"] = "200" }
        });

        // Act
        var result = data
            .GroupBy("batch")
            .Aggregate(new[]
            {
                ("temp", AggregationMethod.Mean),
                ("temp", AggregationMethod.Min),
                ("temp", AggregationMethod.Max),
                ("temp", AggregationMethod.Std)
            });

        // Assert
        var columns = result.ColumnNames.ToList();
        columns.Should().Contain("batch");
        columns.Should().Contain("temp_mean");
        columns.Should().Contain("temp_min");
        columns.Should().Contain("temp_max");
        columns.Should().Contain("temp_std");
    }

    [Fact]
    public void GroupBy_WithSumAggregation_CalculatesTotalCorrectly()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["category"] = "A", ["sales"] = "100" },
            new Dictionary<string, string> { ["category"] = "A", ["sales"] = "200" },
            new Dictionary<string, string> { ["category"] = "B", ["sales"] = "150" }
        });

        // Act
        var result = data
            .GroupBy("category")
            .Aggregate(new[] { ("sales", AggregationMethod.Sum) });

        // Assert
        var rows = result.ToDataFrame().Rows.ToList();
        rows.First(r => r["category"] == "A")["sales_sum"].Should().Be("300");
        rows.First(r => r["category"] == "B")["sales_sum"].Should().Be("150");
    }

    [Fact]
    public void GroupBy_WithCountAggregation_CountsRowsCorrectly()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["group"] = "X", ["value"] = "1" },
            new Dictionary<string, string> { ["group"] = "X", ["value"] = "2" },
            new Dictionary<string, string> { ["group"] = "X", ["value"] = "3" },
            new Dictionary<string, string> { ["group"] = "Y", ["value"] = "4" }
        });

        // Act
        var result = data
            .GroupBy("group")
            .Aggregate(new[] { ("value", AggregationMethod.Count) });

        // Assert
        var rows = result.ToDataFrame().Rows.ToList();
        rows.First(r => r["group"] == "X")["value_count"].Should().Be("3");
        rows.First(r => r["group"] == "Y")["value_count"].Should().Be("1");
    }

    #endregion

    #region Extended Aggregation Methods Tests

    [Fact]
    public void GroupBy_WithVarianceAggregation_CalculatesCorrectly()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["group"] = "A", ["value"] = "2" },
            new Dictionary<string, string> { ["group"] = "A", ["value"] = "4" },
            new Dictionary<string, string> { ["group"] = "A", ["value"] = "6" },
            new Dictionary<string, string> { ["group"] = "A", ["value"] = "8" }
        });

        // Act
        var result = data
            .GroupBy("group")
            .Aggregate(new[] { ("value", AggregationMethod.Var) });

        // Assert
        var variance = double.Parse(result.ToDataFrame().Rows.First()["value_var"]);
        variance.Should().BeApproximately(6.67, 0.01); // Sample variance of [2,4,6,8]
    }

    [Fact]
    public void GroupBy_WithMedianAggregation_CalculatesCorrectly()
    {
        // Arrange - odd count
        var dataOdd = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["group"] = "A", ["value"] = "1" },
            new Dictionary<string, string> { ["group"] = "A", ["value"] = "3" },
            new Dictionary<string, string> { ["group"] = "A", ["value"] = "5" }
        });

        // Arrange - even count
        var dataEven = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["group"] = "B", ["value"] = "2" },
            new Dictionary<string, string> { ["group"] = "B", ["value"] = "4" },
            new Dictionary<string, string> { ["group"] = "B", ["value"] = "6" },
            new Dictionary<string, string> { ["group"] = "B", ["value"] = "8" }
        });

        // Act
        var resultOdd = dataOdd.GroupBy("group").Aggregate(new[] { ("value", AggregationMethod.Median) });
        var resultEven = dataEven.GroupBy("group").Aggregate(new[] { ("value", AggregationMethod.Median) });

        // Assert
        resultOdd.ToDataFrame().Rows.First()["value_median"].Should().Be("3"); // Middle value
        resultEven.ToDataFrame().Rows.First()["value_median"].Should().Be("5"); // Average of 4 and 6
    }

    [Fact]
    public void GroupBy_WithFirstAndLast_ReturnsCorrectValues()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["group"] = "A", ["value"] = "10" },
            new Dictionary<string, string> { ["group"] = "A", ["value"] = "20" },
            new Dictionary<string, string> { ["group"] = "A", ["value"] = "30" }
        });

        // Act
        var result = data
            .GroupBy("group")
            .Aggregate(new[]
            {
                ("value", AggregationMethod.First),
                ("value", AggregationMethod.Last)
            });

        // Assert
        var row = result.ToDataFrame().Rows.First();
        row["value_first"].Should().Be("10");
        row["value_last"].Should().Be("30");
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public void GroupBy_WithInvalidKeyColumn_ThrowsArgumentException()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["col1"] = "A" }
        });

        // Act & Assert
        var act = () => data.GroupBy("nonexistent");
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Column 'nonexistent' not found*");
    }

    [Fact]
    public void GroupBy_WithEmptyKeyColumn_ThrowsArgumentException()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["col1"] = "A" }
        });

        // Act & Assert
        var act = () => data.GroupBy("");
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Key column cannot be empty*");
    }

    [Fact]
    public void Aggregate_WithInvalidColumn_ThrowsArgumentException()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["group"] = "A", ["value"] = "10" }
        });

        // Act & Assert
        var grouped = data.GroupBy("group");
        var act = () => grouped.Aggregate(new[] { ("nonexistent", AggregationMethod.Mean) });
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Column 'nonexistent' not found*");
    }

    [Fact]
    public void Aggregate_WithNoAggregations_ThrowsArgumentException()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["group"] = "A", ["value"] = "10" }
        });

        // Act & Assert
        var grouped = data.GroupBy("group");
        var act = () => grouped.Aggregate(Array.Empty<(string, AggregationMethod)>());
        act.Should().Throw<ArgumentException>()
            .WithMessage("*At least one aggregation must be specified*");
    }

    [Fact]
    public void GroupBy_WithEmptyData_ReturnsEmptyResult()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["group"] = "A", ["value"] = "10" }
        });

        // Filter to empty
        var empty = data.FilterRows(_ => false);

        // Act
        var result = empty
            .GroupBy("group")
            .Aggregate(new[] { ("value", AggregationMethod.Mean) });

        // Assert
        result.RowCount.Should().Be(0);
    }

    [Fact]
    public void GroupBy_WithNullOrEmptyKeys_ExcludesThoseRows()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["group"] = "A", ["value"] = "10" },
            new Dictionary<string, string> { ["group"] = "", ["value"] = "20" },
            new Dictionary<string, string> { ["group"] = "B", ["value"] = "30" }
        });

        // Act
        var result = data
            .GroupBy("group")
            .Aggregate(new[] { ("value", AggregationMethod.Count) });

        // Assert
        result.RowCount.Should().Be(2); // Only "A" and "B", empty key excluded
        result.ToDataFrame().Rows.Should().NotContain(r => r["group"] == "");
    }

    [Fact]
    public void GroupBy_WithNonNumericValues_SkipsInvalidValues()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["group"] = "A", ["value"] = "10" },
            new Dictionary<string, string> { ["group"] = "A", ["value"] = "invalid" },
            new Dictionary<string, string> { ["group"] = "A", ["value"] = "20" }
        });

        // Act
        var result = data
            .GroupBy("group")
            .Aggregate(new[] { ("value", AggregationMethod.Mean) });

        // Assert
        var mean = double.Parse(result.ToDataFrame().Rows.First()["value_mean"]);
        mean.Should().Be(15); // (10 + 20) / 2, "invalid" skipped
    }

    [Fact]
    public void GroupBy_WithAllNonNumericValues_ReturnsEmptyString()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["group"] = "A", ["value"] = "invalid1" },
            new Dictionary<string, string> { ["group"] = "A", ["value"] = "invalid2" }
        });

        // Act
        var result = data
            .GroupBy("group")
            .Aggregate(new[] { ("value", AggregationMethod.Mean) });

        // Assert
        result.ToDataFrame().Rows.First()["value_mean"].Should().BeEmpty();
    }

    #endregion

    #region Custom Suffix and Options Tests

    [Fact]
    public void Aggregate_WithCustomSuffix_UsesCustomFormat()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["group"] = "A", ["value"] = "10" }
        });

        // Act
        var result = data
            .GroupBy("group")
            .Aggregate(
                new[] { ("value", AggregationMethod.Mean) },
                suffixFormat: "_agg_{method}");

        // Assert
        result.ColumnNames.Should().Contain("value_agg_mean");
    }

    [Fact]
    public void Aggregate_WithKeepKeyFalse_ExcludesKeyColumn()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["group"] = "A", ["value"] = "10" },
            new Dictionary<string, string> { ["group"] = "B", ["value"] = "20" }
        });

        // Act
        var result = data
            .GroupBy("group")
            .Aggregate(
                new[] { ("value", AggregationMethod.Mean) },
                keepKey: false);

        // Assert
        result.ColumnNames.Should().NotContain("group");
        result.ColumnNames.Should().Contain("value_mean");
        result.RowCount.Should().Be(2); // Still 2 groups, just no key column
    }

    #endregion

    #region Real-World Dataset Scenario (Dataset 012)

    [Fact]
    public void GroupBy_Dataset012Scenario_AggregatesSensorData()
    {
        // Arrange - Simulating Dataset 012 batch sensor aggregation
        var sensorData = DataPipeline.FromData(new[]
        {
            // Batch A readings
            new Dictionary<string, string> { ["배정번호"] = "A", ["건조로 온도 1 Zone"] = "100.0", ["소입로 온도 1 Zone"] = "200.0" },
            new Dictionary<string, string> { ["배정번호"] = "A", ["건조로 온도 1 Zone"] = "110.0", ["소입로 온도 1 Zone"] = "210.0" },
            new Dictionary<string, string> { ["배정번호"] = "A", ["건조로 온도 1 Zone"] = "105.0", ["소입로 온도 1 Zone"] = "205.0" },
            // Batch B readings
            new Dictionary<string, string> { ["배정번호"] = "B", ["건조로 온도 1 Zone"] = "150.0", ["소입로 온도 1 Zone"] = "250.0" },
            new Dictionary<string, string> { ["배정번호"] = "B", ["건조로 온도 1 Zone"] = "160.0", ["소입로 온도 1 Zone"] = "260.0" }
        });

        // Act - Multiple aggregations per column (as per real use case)
        var result = sensorData
            .GroupBy("배정번호")
            .Aggregate(new[]
            {
                ("건조로 온도 1 Zone", AggregationMethod.Mean),
                ("건조로 온도 1 Zone", AggregationMethod.Std),
                ("소입로 온도 1 Zone", AggregationMethod.Min),
                ("소입로 온도 1 Zone", AggregationMethod.Max),
                ("소입로 온도 1 Zone", AggregationMethod.Mean)
            });

        // Assert
        var rows = result.ToDataFrame().Rows.ToList();
        rows.Should().HaveCount(2); // 2 batches

        var batchA = rows.First(r => r["배정번호"] == "A");
        double.Parse(batchA["건조로 온도 1 Zone_mean"]).Should().BeApproximately(105, 0.1);
        double.Parse(batchA["소입로 온도 1 Zone_min"]).Should().Be(200);
        double.Parse(batchA["소입로 온도 1 Zone_max"]).Should().Be(210);

        var batchB = rows.First(r => r["배정번호"] == "B");
        double.Parse(batchB["건조로 온도 1 Zone_mean"]).Should().Be(155);
        double.Parse(batchB["소입로 온도 1 Zone_mean"]).Should().Be(255);
    }

    #endregion

    #region Performance and Large Dataset Tests

    [Fact]
    public void GroupBy_WithLargeDataset_PerformsEfficiently()
    {
        // Arrange - 10,000 rows, 100 groups
        var random = new Random(42);
        var largeData = Enumerable.Range(1, 10000)
            .Select(i => new Dictionary<string, string>
            {
                ["group_id"] = $"Group_{i % 100}",
                ["value1"] = random.Next(0, 1000).ToString(),
                ["value2"] = random.Next(0, 1000).ToString()
            });

        var pipeline = DataPipeline.FromData(largeData);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = pipeline
            .GroupBy("group_id")
            .Aggregate(new[]
            {
                ("value1", AggregationMethod.Mean),
                ("value1", AggregationMethod.Std),
                ("value2", AggregationMethod.Min),
                ("value2", AggregationMethod.Max)
            });
        stopwatch.Stop();

        // Assert
        result.RowCount.Should().Be(100); // 100 unique groups
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000); // Should complete in < 1 second
    }

    #endregion
}
