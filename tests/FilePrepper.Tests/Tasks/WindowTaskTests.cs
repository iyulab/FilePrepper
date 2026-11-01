using FilePrepper.Pipeline;
using FilePrepper.Tasks;
using FilePrepper.Tasks.WindowOps;
using Microsoft.Extensions.Logging;
using Moq;

namespace FilePrepper.Tests.Tasks;

/// <summary>
/// <c>window</c> had no test. Resample wrote a header from its first window's keys, so a column with no value in that
/// window was missing from the file with every later window's values; it divided by zero for <c>0T</c>, dropped rows with
/// no readable time without a word, and wrote a mistyped column as an empty one.
/// </summary>
public class WindowTaskTests : TaskBaseTest<WindowTask>
{
    public WindowTaskTests(ITestOutputHelper output) : base(output)
    {
    }

    private bool Run(WindowType type, string[] columns, string? window = "5T", AggregationMethod method = AggregationMethod.Mean, int windowSize = 3)
    {
        var option = new WindowOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Type = type,
            Method = method,
            TargetColumns = columns,
            TimeColumn = "ts",
            Window = window,
            WindowSize = windowSize
        };
        return new WindowTask(_mockLogger.Object).Execute(new TaskContext(option));
    }

    private void VerifyWarning(string containing) =>
        _mockLogger.Verify(logger => logger.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => $"{state}".Contains(containing, StringComparison.Ordinal)),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);

    [Fact]
    public void A_column_with_no_value_in_the_first_window_keeps_its_place_and_its_later_values()
    {
        WriteTestFileLines(
            "ts,v,w",
            "2026-01-01 00:00:00,1,",
            "2026-01-01 00:02:00,2,",
            "2026-01-01 00:06:00,4,60",
            "2026-01-01 00:08:00,5,80");

        Assert.True(Run(WindowType.Resample, ["v", "w"]));

        Assert.Equal(["ts,v,w", "2026-01-01 00:00:00,1.5,", "2026-01-01 00:05:00,4.5,70"], ReadOutputFileLines());
    }

    [Fact]
    public void A_target_with_no_number_anywhere_is_written_empty_and_reported()
    {
        WriteTestFileLines(
            "ts,label",
            "2026-01-01 00:00:00,a",
            "2026-01-01 00:06:00,b");

        Assert.True(Run(WindowType.Resample, ["label"]));

        Assert.Equal(["ts,label", "2026-01-01 00:00:00,", "2026-01-01 00:05:00,"], ReadOutputFileLines());
        VerifyWarning("has no numeric value to aggregate");
    }

    [Fact]
    public void A_rolling_column_that_does_not_exist_is_reported_not_written_as_an_unexplained_empty_column()
    {
        WriteTestFileLines(
            "ts,v",
            "2026-01-01 00:00:00,1",
            "2026-01-01 00:01:00,2");

        Assert.True(Run(WindowType.Rolling, ["nope"], window: null));

        VerifyWarning("'nope_rolling' is empty in every row");
    }

    [Fact]
    public void Rows_with_no_readable_time_are_counted_in_a_warning_and_left_out()
    {
        WriteTestFileLines(
            "ts,v",
            "2026-01-01 00:00:00,1",
            "not-a-date,99",
            ",98",
            "2026-01-01 00:02:00,3");

        Assert.True(Run(WindowType.Resample, ["v"]));

        Assert.Equal(["ts,v", "2026-01-01 00:00:00,2"], ReadOutputFileLines());
        VerifyWarning("2 of 4 row(s) left out");
        VerifyWarning("first: data row 2, value 'not-a-date'");
    }

    [Fact]
    public void A_resample_in_which_no_row_has_a_readable_time_is_refused()
    {
        WriteTestFileLines(
            "ts,v",
            "x,1",
            "y,2");

        var error = Assert.Throws<InvalidOperationException>(() => Run(WindowType.Resample, ["v"]));

        Assert.Contains("None of the 2 row(s)", error.Message);
        Assert.Equal(0, new FileInfo(_testOutputPath).Length);
    }

    [Theory]
    [InlineData("0T", "empty")]
    [InlineData("0H", "empty")]
    [InlineData("99999999999D", "too large")]
    [InlineData("5t", "Invalid window format")]
    [InlineData("T", "Invalid window format")]
    [InlineData("-5T", "Invalid window format")]
    public void A_window_that_is_not_a_positive_span_is_a_validation_error(string window, string message)
    {
        var option = new WindowOption { Type = WindowType.Resample, TargetColumns = ["v"], TimeColumn = "ts", Window = window };

        Assert.Contains(option.Validate(), e => e.Contains(message, StringComparison.Ordinal));
        Assert.Throws<ArgumentException>(() => DataPipeline.FromData([]).Resample("ts", window, AggregationMethod.Mean, ["v"]));
    }

    [Theory]
    [InlineData("5T", "2026-01-01 00:04:00", "2026-01-01 00:05:00")]
    [InlineData("2H", "2026-01-01 01:59:00", "2026-01-01 02:00:00")]
    [InlineData("1D", "2026-01-01 23:59:00", "2026-01-02 00:00:00")]
    public void A_window_is_a_number_of_minutes_hours_or_days(string window, string lastInside, string firstOutside)
    {
        var option = new WindowOption { Type = WindowType.Resample, TargetColumns = ["v"], TimeColumn = "ts", Window = window };
        Assert.DoesNotContain(option.Validate(), e => e.Contains("indow", StringComparison.Ordinal));

        var rows = new List<Dictionary<string, string>>
        {
            new() { ["ts"] = "2026-01-01 00:00:00", ["v"] = "1" },
            new() { ["ts"] = lastInside, ["v"] = "3" },
            new() { ["ts"] = firstOutside, ["v"] = "10" }
        };
        var table = DataPipeline.FromData(rows).Resample("ts", window, AggregationMethod.Sum, ["v"]).ToDataFrame();

        Assert.Equal(["4", "10"], table.GetColumn("v").ToList());
    }

    [Fact]
    public void A_rolling_window_aggregates_the_rows_there_are_so_far_at_the_start_and_skips_blank_cells()
    {
        WriteTestFileLines(
            "ts,v",
            "2026-01-01 00:00:00,1",
            "2026-01-01 00:01:00,",
            "2026-01-01 00:02:00,3",
            "2026-01-01 00:03:00,5");

        Assert.True(Run(WindowType.Rolling, ["v"], window: null, method: AggregationMethod.Sum, windowSize: 3));

        // Row 1: itself. Row 2: the blank adds nothing. Row 3: 1 + (blank) + 3. Row 4: (blank) + 3 + 5 - the blank still takes a place.
        Assert.Equal(
            ["ts,v,v_rolling",
             "2026-01-01 00:00:00,1,1",
             "2026-01-01 00:01:00,,1",
             "2026-01-01 00:02:00,3,4",
             "2026-01-01 00:03:00,5,8"],
            ReadOutputFileLines());
    }
}
