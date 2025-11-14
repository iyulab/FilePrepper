using FilePrepper.Pipeline;
using FilePrepper.Tasks.NormalizeData;
using FluentAssertions;
using Xunit;

namespace FilePrepper.Tests.Pipeline;

public class StatisticalFunctionsTests
{
    #region GetStatistics Tests

    [Fact]
    public void GetStatistics_WithValidNumericData_CalculatesCorrectly()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["value"] = "10" },
            new Dictionary<string, string> { ["value"] = "20" },
            new Dictionary<string, string> { ["value"] = "30" },
            new Dictionary<string, string> { ["value"] = "40" },
            new Dictionary<string, string> { ["value"] = "50" }
        });

        // Act
        var stats = data.GetStatistics("value");

        // Assert
        stats.Mean.Should().Be(30); // (10+20+30+40+50)/5 = 30
        stats.Min.Should().Be(10);
        stats.Max.Should().Be(50);
        stats.Median.Should().Be(30); // Middle value
        stats.Q1.Should().Be(20); // 25th percentile
        stats.Q3.Should().Be(40); // 75th percentile
        stats.IQR.Should().Be(20); // Q3 - Q1 = 40 - 20
        stats.Count.Should().Be(5);
        stats.NullCount.Should().Be(0);
        stats.Std.Should().BeApproximately(15.811, 0.001); // Sample std
    }

    [Fact]
    public void GetStatistics_WithNullAndNonNumericValues_ExcludesThem()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["value"] = "10" },
            new Dictionary<string, string> { ["value"] = "" },
            new Dictionary<string, string> { ["value"] = "20" },
            new Dictionary<string, string> { ["value"] = "invalid" },
            new Dictionary<string, string> { ["value"] = "30" }
        });

        // Act
        var stats = data.GetStatistics("value");

        // Assert
        stats.Count.Should().Be(3); // Only 10, 20, 30
        stats.NullCount.Should().Be(2); // Empty and "invalid"
        stats.Mean.Should().Be(20); // (10+20+30)/3
        stats.Min.Should().Be(10);
        stats.Max.Should().Be(30);
    }

    [Fact]
    public void GetStatistics_WithSingleValue_ReturnsZeroStd()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["value"] = "42" }
        });

        // Act
        var stats = data.GetStatistics("value");

        // Assert
        stats.Mean.Should().Be(42);
        stats.Std.Should().Be(0);
        stats.Variance.Should().Be(0);
        stats.Min.Should().Be(42);
        stats.Max.Should().Be(42);
        stats.Median.Should().Be(42);
    }

    [Fact]
    public void GetStatistics_WithInvalidColumn_ThrowsArgumentException()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["value"] = "10" }
        });

        // Act & Assert
        Action act = () => data.GetStatistics("invalid_column");
        act.Should().Throw<ArgumentException>()
            .WithMessage("*invalid_column*not found*");
    }

    [Fact]
    public void GetStatistics_WithNoValidNumericValues_ThrowsInvalidOperationException()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["value"] = "not a number" },
            new Dictionary<string, string> { ["value"] = "" },
            new Dictionary<string, string> { ["value"] = "also invalid" }
        });

        // Act & Assert
        Action act = () => data.GetStatistics("value");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*no valid numeric values*");
    }

    [Fact]
    public void GetStatistics_WithFloatingPointValues_CalculatesCorrectly()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["temp"] = "98.6" },
            new Dictionary<string, string> { ["temp"] = "98.7" },
            new Dictionary<string, string> { ["temp"] = "98.8" },
            new Dictionary<string, string> { ["temp"] = "98.9" },
            new Dictionary<string, string> { ["temp"] = "99.0" }
        });

        // Act
        var stats = data.GetStatistics("temp");

        // Assert
        stats.Mean.Should().BeApproximately(98.8, 0.01);
        stats.Min.Should().Be(98.6);
        stats.Max.Should().Be(99.0);
        stats.Median.Should().Be(98.8);
    }

    [Fact]
    public void GetStatistics_PercentileCalculation_UsesLinearInterpolation()
    {
        // Arrange - Even number of values for interpolation test
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["value"] = "1" },
            new Dictionary<string, string> { ["value"] = "2" },
            new Dictionary<string, string> { ["value"] = "3" },
            new Dictionary<string, string> { ["value"] = "4" }
        });

        // Act
        var stats = data.GetStatistics("value");

        // Assert
        // Median with 4 values: interpolation between 2 and 3
        stats.Median.Should().Be(2.5);
        // Q1: position = 0.25 * 3 = 0.75 → interpolate between index 0 and 1
        stats.Q1.Should().Be(1.75); // 1 + 0.75 * (2-1) = 1.75
        // Q3: position = 0.75 * 3 = 2.25 → interpolate between index 2 and 3
        stats.Q3.Should().Be(3.25); // 3 + 0.25 * (4-3) = 3.25
    }

    #endregion

    #region Normalize Tests - ZScore

    [Fact]
    public void Normalize_ZScore_TransformsCorrectly()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["value"] = "10" },
            new Dictionary<string, string> { ["value"] = "20" },
            new Dictionary<string, string> { ["value"] = "30" }
        });

        // Act
        var normalized = data.Normalize("value", NormalizationMethod.ZScore);

        // Assert
        var rows = normalized.ToDataFrame().Rows.ToList();
        rows.Should().HaveCount(3);

        // Mean = 20, Std = 10
        // (10-20)/10 = -1, (20-20)/10 = 0, (30-20)/10 = 1
        double.Parse(rows[0]["value_normalized"]).Should().BeApproximately(-1.0, 0.01);
        double.Parse(rows[1]["value_normalized"]).Should().BeApproximately(0.0, 0.01);
        double.Parse(rows[2]["value_normalized"]).Should().BeApproximately(1.0, 0.01);
    }

    [Fact]
    public void Normalize_ZScore_WithConstantValues_ThrowsInvalidOperationException()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["value"] = "42" },
            new Dictionary<string, string> { ["value"] = "42" },
            new Dictionary<string, string> { ["value"] = "42" }
        });

        // Act & Assert
        Action act = () => data.Normalize("value", NormalizationMethod.ZScore);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*zero standard deviation*");
    }

    [Fact]
    public void Normalize_ZScore_PreservesOriginalColumn()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["id"] = "1", ["value"] = "10" },
            new Dictionary<string, string> { ["id"] = "2", ["value"] = "20" }
        });

        // Act
        var normalized = data.Normalize("value", NormalizationMethod.ZScore);

        // Assert
        var columns = normalized.ColumnNames.ToList();
        columns.Should().Contain("id");
        columns.Should().Contain("value");
        columns.Should().Contain("value_normalized");
    }

    #endregion

    #region Normalize Tests - MinMax

    [Fact]
    public void Normalize_MinMax_ScalesToZeroOne()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["value"] = "10" },
            new Dictionary<string, string> { ["value"] = "30" },
            new Dictionary<string, string> { ["value"] = "50" }
        });

        // Act
        var normalized = data.Normalize("value", NormalizationMethod.MinMax);

        // Assert
        var rows = normalized.ToDataFrame().Rows.ToList();

        // Min = 10, Max = 50, range = 40
        // (10-10)/40 = 0, (30-10)/40 = 0.5, (50-10)/40 = 1
        double.Parse(rows[0]["value_normalized"]).Should().BeApproximately(0.0, 0.01);
        double.Parse(rows[1]["value_normalized"]).Should().BeApproximately(0.5, 0.01);
        double.Parse(rows[2]["value_normalized"]).Should().BeApproximately(1.0, 0.01);
    }

    [Fact]
    public void Normalize_MinMax_WithConstantValues_ThrowsInvalidOperationException()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["value"] = "100" },
            new Dictionary<string, string> { ["value"] = "100" }
        });

        // Act & Assert
        Action act = () => data.Normalize("value", NormalizationMethod.MinMax);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*constant values*");
    }

    #endregion

    #region Normalize Tests - Robust

    [Fact]
    public void Normalize_Robust_UsesMedianAndIQR()
    {
        // Arrange - Using values where median and IQR are easy to calculate
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["value"] = "10" },
            new Dictionary<string, string> { ["value"] = "20" },
            new Dictionary<string, string> { ["value"] = "30" },
            new Dictionary<string, string> { ["value"] = "40" },
            new Dictionary<string, string> { ["value"] = "50" }
        });

        // Act
        var normalized = data.Normalize("value", NormalizationMethod.Robust);

        // Assert
        var rows = normalized.ToDataFrame().Rows.ToList();

        // Median = 30, Q1 = 20, Q3 = 40, IQR = 20
        // (10-30)/20 = -1, (30-30)/20 = 0, (50-30)/20 = 1
        double.Parse(rows[0]["value_normalized"]).Should().BeApproximately(-1.0, 0.01);
        double.Parse(rows[2]["value_normalized"]).Should().BeApproximately(0.0, 0.01);
        double.Parse(rows[4]["value_normalized"]).Should().BeApproximately(1.0, 0.01);
    }

    [Fact]
    public void Normalize_Robust_WithZeroIQR_ThrowsInvalidOperationException()
    {
        // Arrange - All values the same
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["value"] = "50" },
            new Dictionary<string, string> { ["value"] = "50" },
            new Dictionary<string, string> { ["value"] = "50" }
        });

        // Act & Assert
        Action act = () => data.Normalize("value", NormalizationMethod.Robust);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*zero IQR*");
    }

    #endregion

    #region Normalize - General Tests

    [Fact]
    public void Normalize_WithCustomOutputColumn_UsesSpecifiedName()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["temperature"] = "100" },
            new Dictionary<string, string> { ["temperature"] = "200" }
        });

        // Act
        var normalized = data.Normalize("temperature", NormalizationMethod.MinMax, "temp_scaled");

        // Assert
        var columns = normalized.ColumnNames.ToList();
        columns.Should().Contain("temp_scaled");
        columns.Should().NotContain("temperature_normalized");
    }

    [Fact]
    public void Normalize_WithInvalidColumn_ThrowsArgumentException()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["value"] = "10" }
        });

        // Act & Assert
        Action act = () => data.Normalize("nonexistent", NormalizationMethod.ZScore);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*nonexistent*not found*");
    }

    [Fact]
    public void Normalize_WithNullValues_PreservesAsEmpty()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["value"] = "10" },
            new Dictionary<string, string> { ["value"] = "" },
            new Dictionary<string, string> { ["value"] = "30" }
        });

        // Act
        var normalized = data.Normalize("value", NormalizationMethod.MinMax);

        // Assert
        var rows = normalized.ToDataFrame().Rows.ToList();
        rows[1]["value_normalized"].Should().BeEmpty(); // Null preserved as empty
    }

    [Fact]
    public void Normalize_ChainedNormalizations_WorksCorrectly()
    {
        // Arrange
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["temp"] = "100", ["pressure"] = "10" },
            new Dictionary<string, string> { ["temp"] = "200", ["pressure"] = "20" }
        });

        // Act
        var normalized = data
            .Normalize("temp", NormalizationMethod.ZScore, "temp_z")
            .Normalize("pressure", NormalizationMethod.MinMax, "pressure_scaled");

        // Assert
        var columns = normalized.ColumnNames.ToList();
        columns.Should().Contain("temp");
        columns.Should().Contain("pressure");
        columns.Should().Contain("temp_z");
        columns.Should().Contain("pressure_scaled");

        var rows = normalized.ToDataFrame().Rows.ToList();
        rows.Should().HaveCount(2);
    }

    #endregion

    #region Real-World Dataset Tests

    [Fact]
    public void GetStatistics_Dataset012Scenario_CalculatesSensorStats()
    {
        // Arrange - Simulating sensor temperature data from Dataset 012
        var sensorData = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["배정번호"] = "A", ["건조로온도"] = "850.5" },
            new Dictionary<string, string> { ["배정번호"] = "A", ["건조로온도"] = "852.3" },
            new Dictionary<string, string> { ["배정번호"] = "A", ["건조로온도"] = "851.1" },
            new Dictionary<string, string> { ["배정번호"] = "B", ["건조로온도"] = "849.8" },
            new Dictionary<string, string> { ["배정번호"] = "B", ["건조로온도"] = "850.2" }
        });

        // Act
        var stats = sensorData.GetStatistics("건조로온도");

        // Assert
        stats.Count.Should().Be(5);
        stats.Mean.Should().BeApproximately(850.78, 0.01);
        stats.Min.Should().Be(849.8);
        stats.Max.Should().Be(852.3);
        stats.Std.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Normalize_Dataset012Scenario_NormalizesSensorReadings()
    {
        // Arrange
        var sensorData = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["sensor_id"] = "1", ["temperature"] = "800" },
            new Dictionary<string, string> { ["sensor_id"] = "2", ["temperature"] = "850" },
            new Dictionary<string, string> { ["sensor_id"] = "3", ["temperature"] = "900" }
        });

        // Act
        var normalized = sensorData.Normalize("temperature", NormalizationMethod.ZScore);

        // Assert
        var rows = normalized.ToDataFrame().Rows.ToList();
        rows.Should().HaveCount(3);

        // All normalized values should be preserved
        rows.All(r => r.ContainsKey("temperature_normalized")).Should().BeTrue();
        rows.All(r => !string.IsNullOrEmpty(r["temperature_normalized"])).Should().BeTrue();
    }

    #endregion

    #region Performance Tests

    [Fact]
    public void GetStatistics_WithLargeDataset_PerformsEfficiently()
    {
        // Arrange - 10K rows
        var rows = Enumerable.Range(1, 10000)
            .Select(i => new Dictionary<string, string> { ["value"] = i.ToString() })
            .ToArray();

        var data = DataPipeline.FromData(rows);

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var stats = data.GetStatistics("value");
        sw.Stop();

        // Assert
        stats.Count.Should().Be(10000);
        stats.Mean.Should().BeApproximately(5000.5, 0.1);
        sw.ElapsedMilliseconds.Should().BeLessThan(100); // Should be fast
    }

    [Fact]
    public void Normalize_WithLargeDataset_PerformsEfficiently()
    {
        // Arrange - 10K rows
        var rows = Enumerable.Range(1, 10000)
            .Select(i => new Dictionary<string, string> { ["id"] = i.ToString(), ["value"] = (i * 10).ToString() })
            .ToArray();

        var data = DataPipeline.FromData(rows);

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var normalized = data.Normalize("value", NormalizationMethod.ZScore);
        sw.Stop();

        // Assert
        var resultRows = normalized.ToDataFrame().Rows.ToList();
        resultRows.Should().HaveCount(10000);
        sw.ElapsedMilliseconds.Should().BeLessThan(200); // Should be reasonably fast
    }

    #endregion
}
