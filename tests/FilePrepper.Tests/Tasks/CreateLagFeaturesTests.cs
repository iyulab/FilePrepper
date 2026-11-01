using FilePrepper.Tasks;
using FilePrepper.Tasks.CreateLagFeatures;
using Microsoft.Extensions.Logging;
using Moq;

namespace FilePrepper.Tests.Tasks;

/// <summary>
/// <c>create-lag-features</c> had no test. It ordered the time column as text, so with a sequence 1..12 the row after 1 was 10 and
/// the lag of 2 was the value of 12: every lag of a numeric time was the value of the wrong row, with a success.
/// </summary>
public class CreateLagFeaturesTests : TaskBaseTest<CreateLagFeaturesTask>
{
    public CreateLagFeaturesTests(ITestOutputHelper output) : base(output)
    {
    }

    private string[] Run(int[] periods, bool dropNull = true, params string[] keep)
    {
        var option = new CreateLagFeaturesOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            GroupByColumn = "g",
            TimeColumn = "t",
            LagColumns = ["v"],
            LagPeriods = [.. periods],
            DropNullRows = dropNull,
            KeepColumns = [.. keep]
        };
        Assert.True(new CreateLagFeaturesTask(_mockLogger.Object).Execute(new TaskContext(option)));
        return ReadOutputFileLines();
    }

    private void VerifyWarning(string containing) =>
        _mockLogger.Verify(logger => logger.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => $"{state}".Contains(containing, StringComparison.Ordinal)),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);

    [Fact]
    public void A_numeric_time_is_ordered_as_numbers_not_as_text()
    {
        // Shuffled 1..12: as text the order is 1,10,11,12,2,3,... and the lag of 2 was the value of 12.
        var order = new[] { 9, 2, 12, 1, 5, 10, 3, 11, 4, 8, 7, 6 };
        WriteTestFileLines(["g,t,v", .. order.Select(t => $"A,{t},{t * 10}")]);

        var lines = Run([1], keep: "t");

        Assert.Equal(
            ["g,t,v_lag1", "A,2,10", "A,3,20", "A,4,30", "A,5,40", "A,6,50", "A,7,60", "A,8,70", "A,9,80", "A,10,90", "A,11,100", "A,12,110"],
            lines);
    }

    [Fact]
    public void Dates_are_ordered_as_dates_whatever_their_written_form()
    {
        // 01/02/2026 is January 2nd (month first); as text it would come before 01/10/2026 only by accident of the digits.
        WriteTestFileLines(
            "g,t,v",
            "A,01/10/2026,3",
            "A,01/02/2026,1",
            "A,01/09/2026,2");

        Assert.Equal(["g,t,v_lag1", "A,01/09/2026,1", "A,01/10/2026,2"], Run([1], keep: "t"));
    }

    [Fact]
    public void Text_that_is_neither_numbers_nor_dates_is_ordered_as_text_and_the_log_says_so()
    {
        WriteTestFileLines(
            "g,t,v",
            "A,b,2",
            "A,a,1",
            "A,c,3");

        Assert.Equal(["g,t,v_lag1", "A,b,1", "A,c,2"], Run([1], keep: "t"));
        VerifyWarning("ordered as text");
    }

    [Fact]
    public void Each_group_is_ordered_and_lagged_on_its_own()
    {
        WriteTestFileLines(
            "g,t,v",
            "B,2,200",
            "A,2,20",
            "B,1,100",
            "A,1,10");

        Assert.Equal(["g,t,v_lag1", "B,2,100", "A,2,10"], Run([1], keep: "t"));
    }

    [Fact]
    public void A_lag_counts_rows_in_the_group_not_time_units()
    {
        // Times 1, 2, 10: the lag-1 of 10 is the value at 2, not a missing value for time 9.
        WriteTestFileLines(
            "g,t,v",
            "A,1,5",
            "A,2,6",
            "A,10,7");

        Assert.Equal(["g,t,v_lag1", "A,2,5", "A,10,6"], Run([1], keep: "t"));
    }

    [Fact]
    public void Rows_without_enough_history_are_dropped_or_kept_empty_as_asked()
    {
        WriteTestFileLines(
            "g,t,v",
            "A,1,5",
            "A,2,6",
            "A,3,7");

        Assert.Equal(["g,v_lag1,v_lag2", "A,6,5"], Run([1, 2]));
        Assert.Equal(["g,v_lag1,v_lag2", "A,,", "A,5,", "A,6,5"], Run([1, 2], dropNull: false));
    }

    [Fact]
    public void Blank_and_repeated_times_are_counted_in_warnings()
    {
        WriteTestFileLines(
            "g,t,v",
            "A,1,5",
            "A,1,6",
            "A,,7");

        Run([1], dropNull: false);

        VerifyWarning("1 of 3 row(s) have no value in time column 't'");
        VerifyWarning("1 row(s) repeat the time of the row before them");
    }
}
