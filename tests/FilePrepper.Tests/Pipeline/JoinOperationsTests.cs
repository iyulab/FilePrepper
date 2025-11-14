using FilePrepper.Pipeline;
using FluentAssertions;
using Xunit;

namespace FilePrepper.Tests.Pipeline;

/// <summary>
/// Comprehensive tests for Join operations
/// Covers: Inner, Left, Right, Outer joins, column selection, prefixes, edge cases
/// </summary>
public class JoinOperationsTests
{
    #region Inner Join Tests

    [Fact]
    public void Join_InnerJoin_ReturnsOnlyMatchingRows()
    {
        // Arrange
        var left = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "1", ["name"] = "Alice" },
            new Dictionary<string, string> { ["id"] = "2", ["name"] = "Bob" },
            new Dictionary<string, string> { ["id"] = "3", ["name"] = "Charlie" }
        });

        var right = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "1", ["value"] = "100" },
            new Dictionary<string, string> { ["id"] = "2", ["value"] = "200" }
            // No row for id=3
        });

        // Act
        var result = left.Join(right, "id", "id", JoinType.Inner);

        // Assert
        var rows = result.ToDataFrame().Rows.ToList();
        rows.Should().HaveCount(2); // Only matching rows
        rows.Should().Contain(r => r["id"] == "1" && r["name"] == "Alice" && r["value"] == "100");
        rows.Should().Contain(r => r["id"] == "2" && r["name"] == "Bob" && r["value"] == "200");
        rows.Should().NotContain(r => r["id"] == "3"); // Unmatched row excluded
    }

    [Fact]
    public void Join_InnerJoin_WithDuplicateKeys_CreatesCartesianProduct()
    {
        // Arrange - 1:N join (1 left row matches 2 right rows)
        var left = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "1", ["name"] = "Alice" }
        });

        var right = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "1", ["score"] = "85" },
            new Dictionary<string, string> { ["id"] = "1", ["score"] = "90" }
        });

        // Act
        var result = left.Join(right, "id", "id", JoinType.Inner);

        // Assert
        var rows = result.ToDataFrame().Rows.ToList();
        rows.Should().HaveCount(2); // Cartesian product: 1 × 2 = 2
        rows.Should().AllSatisfy(r => r["name"].Should().Be("Alice"));
        rows.Should().Contain(r => r["score"] == "85");
        rows.Should().Contain(r => r["score"] == "90");
    }

    #endregion

    #region Left Join Tests

    [Fact]
    public void Join_LeftJoin_IncludesAllLeftRows()
    {
        // Arrange
        var left = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "1", ["name"] = "Alice" },
            new Dictionary<string, string> { ["id"] = "2", ["name"] = "Bob" },
            new Dictionary<string, string> { ["id"] = "3", ["name"] = "Charlie" }
        });

        var right = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "1", ["value"] = "100" }
            // Only id=1 has a match
        });

        // Act
        var result = left.Join(right, "id", "id", JoinType.Left);

        // Assert
        var rows = result.ToDataFrame().Rows.ToList();
        rows.Should().HaveCount(3); // All left rows included
        rows.First(r => r["id"] == "1")["value"].Should().Be("100"); // Matched
        rows.First(r => r["id"] == "2")["value"].Should().BeEmpty(); // Unmatched (null)
        rows.First(r => r["id"] == "3")["value"].Should().BeEmpty(); // Unmatched (null)
    }

    [Fact]
    public void Join_LeftJoin_WithNoMatches_KeepsAllLeftRows()
    {
        // Arrange
        var left = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "1", ["name"] = "Alice" }
        });

        var right = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "999", ["value"] = "100" }
        });

        // Act
        var result = left.Join(right, "id", "id", JoinType.Left);

        // Assert
        var rows = result.ToDataFrame().Rows.ToList();
        rows.Should().HaveCount(1); // Left row preserved
        rows.First()["id"].Should().Be("1");
        rows.First()["value"].Should().BeEmpty(); // No match -> empty
    }

    #endregion

    #region Right Join Tests

    [Fact]
    public void Join_RightJoin_IncludesAllRightRows()
    {
        // Arrange
        var left = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "1", ["name"] = "Alice" }
        });

        var right = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "1", ["value"] = "100" },
            new Dictionary<string, string> { ["id"] = "2", ["value"] = "200" },
            new Dictionary<string, string> { ["id"] = "3", ["value"] = "300" }
        });

        // Act
        var result = left.Join(right, "id", "id", JoinType.Right);

        // Assert
        var rows = result.ToDataFrame().Rows.ToList();
        rows.Should().HaveCount(3); // All right rows included
        rows.First(r => r["id"] == "1")["name"].Should().Be("Alice"); // Matched
        rows.First(r => r["id"] == "2")["name"].Should().BeEmpty(); // Unmatched (null)
        rows.First(r => r["id"] == "3")["name"].Should().BeEmpty(); // Unmatched (null)
    }

    #endregion

    #region Outer Join Tests

    [Fact]
    public void Join_OuterJoin_IncludesAllRowsFromBothSides()
    {
        // Arrange
        var left = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "1", ["name"] = "Alice" },
            new Dictionary<string, string> { ["id"] = "2", ["name"] = "Bob" }
        });

        var right = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "2", ["value"] = "200" },
            new Dictionary<string, string> { ["id"] = "3", ["value"] = "300" }
        });

        // Act
        var result = left.Join(right, "id", "id", JoinType.Outer);

        // Assert
        var rows = result.ToDataFrame().Rows.ToList();
        rows.Should().HaveCount(3); // id=1 (left only), id=2 (matched), id=3 (right only)

        // id=1: left only
        rows.Should().Contain(r => r["id"] == "1" && r["name"] == "Alice" && string.IsNullOrEmpty(r["value"]));

        // id=2: matched
        rows.Should().Contain(r => r["id"] == "2" && r["name"] == "Bob" && r["value"] == "200");

        // id=3: right only
        rows.Should().Contain(r => r["id"] == "3" && string.IsNullOrEmpty(r["name"]) && r["value"] == "300");
    }

    #endregion

    #region Column Selection Tests

    [Fact]
    public void Join_WithSelectColumns_OnlyIncludesSpecifiedColumns()
    {
        // Arrange
        var left = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "1", ["name"] = "Alice" }
        });

        var right = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "1", ["value"] = "100", ["extra1"] = "X", ["extra2"] = "Y" }
        });

        // Act
        var result = left.Join(right, "id", "id", JoinType.Inner, selectColumns: new[] { "value" });

        // Assert
        var columns = result.ColumnNames.ToList();
        columns.Should().Contain("id");
        columns.Should().Contain("name");
        columns.Should().Contain("value");
        columns.Should().NotContain("extra1");
        columns.Should().NotContain("extra2");
    }

    #endregion

    #region Prefix Tests

    [Fact]
    public void Join_WithLeftPrefix_AddsPrefixToLeftColumns()
    {
        // Arrange
        var left = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "1", ["value"] = "L1" }
        });

        var right = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "1", ["value"] = "R1" }
        });

        // Act
        var result = left.Join(right, "id", "id", JoinType.Inner, leftPrefix: "left_");

        // Assert
        var row = result.ToDataFrame().Rows.First();
        row.Should().ContainKey("left_id");
        row.Should().ContainKey("left_value");
        row["left_value"].Should().Be("L1");
        row["value"].Should().Be("R1"); // Right has no prefix
    }

    [Fact]
    public void Join_WithRightPrefix_AddsPrefixToRightColumns()
    {
        // Arrange
        var left = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "1", ["value"] = "L1" }
        });

        var right = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "1", ["value"] = "R1" }
        });

        // Act
        var result = left.Join(right, "id", "id", JoinType.Inner, rightPrefix: "right_");

        // Assert
        var row = result.ToDataFrame().Rows.First();
        row.Should().ContainKey("id"); // Left has no prefix
        row.Should().ContainKey("value"); // Left "value" (no prefix)
        row.Should().ContainKey("right_value"); // Right "value" with prefix
        row["value"].Should().Be("L1");
        row["right_value"].Should().Be("R1");
    }

    [Fact]
    public void Join_WithBothPrefixes_AddsPrefixesToBothSides()
    {
        // Arrange
        var left = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "1", ["name"] = "Alice" }
        });

        var right = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "1", ["name"] = "Bob" }
        });

        // Act
        var result = left.Join(right, "id", "id", JoinType.Inner, leftPrefix: "l_", rightPrefix: "r_");

        // Assert
        var row = result.ToDataFrame().Rows.First();
        row.Should().ContainKey("l_id");
        row.Should().ContainKey("l_name");
        row.Should().ContainKey("r_name");
        row["l_name"].Should().Be("Alice");
        row["r_name"].Should().Be("Bob");
    }

    #endregion

    #region Column Collision Tests

    [Fact]
    public void Join_WithColumnCollision_AutoAddsRightSuffix()
    {
        // Arrange
        var left = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "1", ["value"] = "LeftValue" }
        });

        var right = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "1", ["value"] = "RightValue" }
        });

        // Act - no prefixes specified, collision should be handled
        var result = left.Join(right, "id", "id", JoinType.Inner);

        // Assert
        var row = result.ToDataFrame().Rows.First();
        row["value"].Should().Be("LeftValue"); // Left value keeps original name
        row["value_right"].Should().Be("RightValue"); // Right value gets _right suffix
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public void Join_WithInvalidLeftKey_ThrowsArgumentException()
    {
        // Arrange
        var left = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "1" }
        });

        var right = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "1" }
        });

        // Act & Assert
        var act = () => left.Join(right, "nonexistent", "id", JoinType.Inner);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Left key 'nonexistent' not found*");
    }

    [Fact]
    public void Join_WithInvalidRightKey_ThrowsArgumentException()
    {
        // Arrange
        var left = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "1" }
        });

        var right = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "1" }
        });

        // Act & Assert
        var act = () => left.Join(right, "id", "nonexistent", JoinType.Inner);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Right key 'nonexistent' not found*");
    }

    [Fact]
    public void Join_WithEmptyLeft_ReturnsEmptyResult()
    {
        // Arrange
        var left = DataPipeline.FromData(Array.Empty<Dictionary<string, string>>());

        var right = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "1", ["value"] = "100" }
        });

        // Create dummy pipeline with columns to avoid error
        var leftWithColumns = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "999", ["name"] = "Dummy" }
        }).FilterRows(_ => false); // Empty but with schema

        // Act
        var result = leftWithColumns.Join(right, "id", "id", JoinType.Inner);

        // Assert
        result.RowCount.Should().Be(0);
    }

    [Fact]
    public void Join_WithNullKeys_ExcludesRowsWithNullKeys()
    {
        // Arrange
        var left = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "1", ["name"] = "Alice" },
            new Dictionary<string, string> { ["id"] = "", ["name"] = "NoId" }, // Empty key
            new Dictionary<string, string> { ["id"] = "2", ["name"] = "Bob" }
        });

        var right = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "1", ["value"] = "100" },
            new Dictionary<string, string> { ["id"] = "2", ["value"] = "200" }
        });

        // Act
        var result = left.Join(right, "id", "id", JoinType.Inner);

        // Assert
        var rows = result.ToDataFrame().Rows.ToList();
        rows.Should().HaveCount(2); // Only rows with valid keys
        rows.Should().NotContain(r => r["name"] == "NoId");
    }

    [Fact]
    public void Join_LeftJoin_WithNullLeftKey_IncludesRowIfLeftJoin()
    {
        // Arrange
        var left = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "1", ["name"] = "Alice" },
            new Dictionary<string, string> { ["id"] = "", ["name"] = "NoId" }
        });

        var right = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "1", ["value"] = "100" }
        });

        // Act
        var result = left.Join(right, "id", "id", JoinType.Left);

        // Assert
        var rows = result.ToDataFrame().Rows.ToList();
        rows.Should().HaveCount(2); // Left join includes row with empty key
        rows.Should().Contain(r => r["name"] == "NoId");
    }

    #endregion

    #region Real-World Dataset Scenario (Dataset 012)

    [Fact]
    public void Join_Dataset012Scenario_JoinsSensorDataWithQualityLabels()
    {
        // Arrange - Simulating Dataset 012: Aggregated sensor data + quality labels
        var aggregatedSensorData = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["배정번호"] = "A", ["건조로 온도 1 Zone_mean"] = "105.0", ["소입로 온도 1 Zone_mean"] = "205.0" },
            new Dictionary<string, string> { ["배정번호"] = "B", ["건조로 온도 1 Zone_mean"] = "155.0", ["소입로 온도 1 Zone_mean"] = "255.0" }
        });

        var qualityLabels = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["배정번호"] = "A", ["경도"] = "75.5", ["변형(폭)"] = "0.2", ["양품수량"] = "100" },
            new Dictionary<string, string> { ["배정번호"] = "B", ["경도"] = "80.0", ["변형(폭)"] = "0.1", ["양품수량"] = "120" }
        });

        // Act - Inner join on batch ID, select only quality columns
        var result = aggregatedSensorData.Join(
            qualityLabels,
            leftKey: "배정번호",
            rightKey: "배정번호",
            joinType: JoinType.Inner,
            selectColumns: new[] { "경도", "변형(폭)", "양품수량" });

        // Assert
        var rows = result.ToDataFrame().Rows.ToList();
        rows.Should().HaveCount(2); // 2 batches joined

        var batchA = rows.First(r => r["배정번호"] == "A");
        batchA["건조로 온도 1 Zone_mean"].Should().Be("105.0");
        batchA["경도"].Should().Be("75.5");
        batchA["양품수량"].Should().Be("100");

        var batchB = rows.First(r => r["배정번호"] == "B");
        batchB["건조로 온도 1 Zone_mean"].Should().Be("155.0");
        batchB["경도"].Should().Be("80.0");
    }

    #endregion

    #region Performance Tests

    [Fact]
    public void Join_WithLargeDatasets_PerformsEfficiently()
    {
        // Arrange - 10,000 rows in each dataset
        var random = new Random(42);
        var leftData = Enumerable.Range(1, 10000)
            .Select(i => new Dictionary<string, string>
            {
                ["id"] = $"{i % 5000}", // 50% match rate
                ["left_value"] = random.Next(0, 1000).ToString()
            });

        var rightData = Enumerable.Range(1, 10000)
            .Select(i => new Dictionary<string, string>
            {
                ["id"] = $"{i % 5000}",
                ["right_value"] = random.Next(0, 1000).ToString()
            });

        var left = DataPipeline.FromData(leftData);
        var right = DataPipeline.FromData(rightData);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = left.Join(right, "id", "id", JoinType.Inner);
        stopwatch.Stop();

        // Assert
        result.RowCount.Should().BeGreaterThan(0); // Should have matches
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // Should complete in < 5 seconds
    }

    #endregion
}
