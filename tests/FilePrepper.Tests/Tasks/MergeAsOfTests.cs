using FilePrepper.Tasks;
using FilePrepper.Tasks.MergeAsOf;
using Microsoft.Extensions.Logging;
using Moq;

namespace FilePrepper.Tests.Tasks;

/// <summary>
/// <c>merge-asof</c> had no test. It compared every left row with every right row (a 200,000 by 200,000 join took minutes), took
/// whichever of several right rows with the same time the unstable sort happened to put first, dropped right rows whose time it
/// could not read without a word, called an empty left file "column not found", and carried an option that did nothing.
/// </summary>
public class MergeAsOfTests : TaskBaseTest<MergeAsOfTask>
{
    private readonly string _rightPath = Path.GetTempFileName();

    public MergeAsOfTests(ITestOutputHelper output) : base(output)
    {
    }

    public override void Dispose()
    {
        if (File.Exists(_rightPath)) File.Delete(_rightPath);
        base.Dispose();
    }

    private string[] Run(AsOfDirection direction, double? tolerance = null, string leftOn = "t", string rightOn = "when")
    {
        var option = new MergeAsOfOption
        {
            InputPaths = [_testInputPath, _rightPath],
            OutputPath = _testOutputPath,
            LeftOnColumn = leftOn,
            RightOnColumn = rightOn,
            Direction = direction,
            Tolerance = tolerance
        };
        Assert.True(new MergeAsOfTask(_mockLogger.Object).Execute(new TaskContext(option)));
        return ReadOutputFileLines();
    }

    private void VerifyWarning(string containing) =>
        _mockLogger.Verify(logger => logger.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => $"{state}".Contains(containing, StringComparison.Ordinal)),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);

    private void Files(string[] left, string[] right)
    {
        WriteTestFileLines(left);
        File.WriteAllLines(_rightPath, right);
    }

    [Fact]
    public void Backward_takes_the_latest_time_not_after_the_left_time_and_the_last_row_among_equal_times()
    {
        Files(
            ["t,a", "2026-01-01 00:00:10,1", "2026-01-01 00:00:20,2", "2026-01-01 00:00:05,3"],
            ["when,b", "2026-01-01 00:00:10,first", "2026-01-01 00:00:10,second", "2026-01-01 00:00:25,late"]);

        Assert.Equal(
            ["t,a,b",
             "2026-01-01 00:00:10,1,second",
             "2026-01-01 00:00:20,2,second",
             "2026-01-01 00:00:05,3,"],
            Run(AsOfDirection.Backward));
    }

    [Fact]
    public void Forward_takes_the_earliest_time_not_before_the_left_time_and_the_first_row_among_equal_times()
    {
        Files(
            ["t,a", "2026-01-01 00:00:10,1", "2026-01-01 00:00:11,2", "2026-01-01 00:00:30,3"],
            ["when,b", "2026-01-01 00:00:10,first", "2026-01-01 00:00:10,second", "2026-01-01 00:00:25,late"]);

        Assert.Equal(
            ["t,a,b",
             "2026-01-01 00:00:10,1,first",
             "2026-01-01 00:00:11,2,late",
             "2026-01-01 00:00:30,3,"],
            Run(AsOfDirection.Forward));
    }

    [Fact]
    public void Nearest_takes_the_closer_time_and_the_earlier_one_when_they_are_equally_close()
    {
        Files(
            ["t,a", "2026-01-01 00:00:12,1", "2026-01-01 00:00:18,2", "2026-01-01 00:00:15,3"],
            ["when,b", "2026-01-01 00:00:10,early", "2026-01-01 00:00:20,late"]);

        Assert.Equal(
            ["t,a,b",
             "2026-01-01 00:00:12,1,early",
             "2026-01-01 00:00:18,2,late",
             "2026-01-01 00:00:15,3,early"],
            Run(AsOfDirection.Nearest));
    }

    [Fact]
    public void A_right_file_that_is_not_sorted_is_matched_as_if_it_were()
    {
        Files(
            ["t,a", "2026-01-01 00:00:30,1"],
            ["when,b", "2026-01-01 00:00:50,c", "2026-01-01 00:00:10,a", "2026-01-01 00:00:20,b"]);

        Assert.Equal(["t,a,b", "2026-01-01 00:00:30,1,b"], Run(AsOfDirection.Backward));
    }

    [Fact]
    public void The_tolerance_is_in_seconds_and_inclusive()
    {
        Files(
            ["t,a", "2026-01-01 00:00:13,1", "2026-01-01 00:00:14,2"],
            ["when,b", "2026-01-01 00:00:10,x"]);

        Assert.Equal(["t,a,b", "2026-01-01 00:00:13,1,x", "2026-01-01 00:00:14,2,"], Run(AsOfDirection.Backward, tolerance: 3));
    }

    [Fact]
    public void A_column_of_the_right_file_with_a_left_name_gets_the_suffix_and_the_matching_column_is_not_copied()
    {
        Files(
            ["t,a", "2026-01-01 00:00:10,1"],
            ["when,a,b", "2026-01-01 00:00:10,9,x"]);

        Assert.Equal(["t,a,a_right,b", "2026-01-01 00:00:10,1,9,x"], Run(AsOfDirection.Backward));
    }

    [Fact]
    public void Rows_whose_time_cannot_be_read_are_counted_in_warnings_and_not_lost_silently()
    {
        Files(
            ["t,a", "2026-01-01 00:00:10,1", "bad,2"],
            ["when,b", "2026-01-01 00:00:10,x", "later,y"]);

        Assert.Equal(["t,a,b", "2026-01-01 00:00:10,1,x", "bad,2,"], Run(AsOfDirection.Backward));
        VerifyWarning("1 of 2 left row(s) have no readable time in column 't'");
        VerifyWarning("1 of 2 right row(s) have no readable time in column 'when'");
    }

    [Fact]
    public void An_empty_left_file_gives_the_whole_header_not_a_column_not_found_error()
    {
        Files(["t,a"], ["when,b", "2026-01-01 00:00:10,x"]);

        Assert.Equal(["t,a,b"], Run(AsOfDirection.Backward));
    }

    [Fact]
    public void A_matching_column_that_is_not_in_its_file_is_refused_with_the_columns_listed()
    {
        Files(["t,a", "2026-01-01 00:00:10,1"], ["when,b", "2026-01-01 00:00:10,x"]);
        var option = new MergeAsOfOption { InputPaths = [_testInputPath, _rightPath], OutputPath = _testOutputPath, LeftOnColumn = "tt", RightOnColumn = "when" };

        var error = Assert.Throws<ValidationException>(() => new MergeAsOfTask(_mockLogger.Object).Execute(new TaskContext(option)));

        Assert.Contains("Left column 'tt' not found in the left file (columns: t, a)", error.Message);
    }

    [Fact]
    public void A_large_join_does_not_compare_every_left_row_with_every_right_row()
    {
        // 60,000 x 60,000 compared pairwise is 3.6 billion steps; with a search per left row it is a fraction of a second.
        var start = new DateTime(2026, 1, 1);
        var left = new List<string> { "t,a" };
        var right = new List<string> { "when,b" };
        for (var i = 0; i < 60_000; i++)
        {
            left.Add($"{start.AddSeconds(i * 3 + 1):yyyy-MM-dd HH:mm:ss},{i}");
            right.Add($"{start.AddSeconds(i * 3):yyyy-MM-dd HH:mm:ss},{i * 2}");
        }
        Files([.. left], [.. right]);

        var lines = Run(AsOfDirection.Backward);

        Assert.Equal(60_001, lines.Length);
        Assert.Equal("2026-01-01 00:00:04,1,2", lines[2]);
    }
}
