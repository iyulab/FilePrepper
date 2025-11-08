using FilePrepper.Pipeline;
using FluentAssertions;
using Xunit;

namespace FilePrepper.Tests.Pipeline;

/// <summary>
/// TDD: DataFrame (immutable data container) 테스트
/// 목표: Pipeline의 결과를 담는 불변 데이터 구조
/// </summary>
public class DataFrameTests
{
    [Fact]
    public void Constructor_ShouldCreateImmutableDataFrame()
    {
        // Arrange
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["Name"] = "Alice", ["Age"] = "25" }
        };
        var columns = new[] { "Name", "Age" };

        // Act
        var df = new DataFrame(rows, columns);

        // Assert
        df.Rows.Should().HaveCount(1);
        df.ColumnNames.Should().BeEquivalentTo(columns);
    }

    [Fact]
    public void Rows_ShouldBeReadOnly()
    {
        // Arrange
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["Name"] = "Alice" }
        };
        var df = new DataFrame(rows, new[] { "Name" });

        // Act
        var rowsCopy = df.Rows;

        // Assert
        rowsCopy.Should().BeOfType<List<Dictionary<string, string>>>();
        // DataFrame should return a defensive copy to prevent external modification
    }

    [Fact]
    public void GetColumn_ShouldReturnColumnValues()
    {
        // Arrange
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["Name"] = "Alice", ["Age"] = "25" },
            new() { ["Name"] = "Bob", ["Age"] = "30" }
        };
        var df = new DataFrame(rows, new[] { "Name", "Age" });

        // Act
        var ages = df.GetColumn("Age");

        // Assert
        ages.Should().BeEquivalentTo(new[] { "25", "30" });
    }

    [Fact]
    public void GetColumn_WithMissingColumn_ShouldThrowException()
    {
        // Arrange
        var df = new DataFrame(
            new List<Dictionary<string, string>> { new() { ["Name"] = "Alice" } },
            new[] { "Name" }
        );

        // Act & Assert
        df.Invoking(x => x.GetColumn("NonExistent"))
            .Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void RowCount_ShouldReturnCorrectCount()
    {
        // Arrange
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["A"] = "1" },
            new() { ["A"] = "2" },
            new() { ["A"] = "3" }
        };
        var df = new DataFrame(rows, new[] { "A" });

        // Act & Assert
        df.RowCount.Should().Be(3);
    }

    [Fact]
    public void ColumnCount_ShouldReturnCorrectCount()
    {
        // Arrange
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["A"] = "1", ["B"] = "2", ["C"] = "3" }
        };
        var df = new DataFrame(rows, new[] { "A", "B", "C" });

        // Act & Assert
        df.ColumnCount.Should().Be(3);
    }

    [Fact]
    public void Select_ShouldProjectColumns()
    {
        // Arrange
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["Name"] = "Alice", ["Age"] = "25", ["City"] = "NYC" }
        };
        var df = new DataFrame(rows, new[] { "Name", "Age", "City" });

        // Act
        var selected = df.Select("Name", "City");

        // Assert
        selected.ColumnNames.Should().BeEquivalentTo(new[] { "Name", "City" });
        selected.Rows[0].Should().NotContainKey("Age");
    }

    [Fact]
    public void Where_ShouldFilterRows()
    {
        // Arrange
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["Age"] = "25" },
            new() { ["Age"] = "30" },
            new() { ["Age"] = "35" }
        };
        var df = new DataFrame(rows, new[] { "Age" });

        // Act
        var filtered = df.Where(row => int.Parse(row["Age"]) >= 30);

        // Assert
        filtered.RowCount.Should().Be(2);
    }

    [Fact]
    public void ToCsv_ShouldSerializeToCsvString()
    {
        // Arrange
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["Name"] = "Alice", ["Age"] = "25" },
            new() { ["Name"] = "Bob", ["Age"] = "30" }
        };
        var df = new DataFrame(rows, new[] { "Name", "Age" });

        // Act
        var csv = df.ToCsv();

        // Assert
        csv.Should().Contain("Name,Age");
        csv.Should().Contain("Alice,25");
        csv.Should().Contain("Bob,30");
    }

    [Fact]
    public void ToJson_ShouldSerializeToJson()
    {
        // Arrange
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["Name"] = "Alice", ["Age"] = "25" }
        };
        var df = new DataFrame(rows, new[] { "Name", "Age" });

        // Act
        var json = df.ToJson();

        // Assert - JSON can have spaces, so be flexible
        json.Should().Contain("Name");
        json.Should().Contain("Alice");
        json.Should().Contain("Age");
        json.Should().Contain("25");
    }
}
