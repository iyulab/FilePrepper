using FilePrepper.Tasks;
using FilePrepper.Tasks.DateTimeOps;
using FilePrepper.Tasks.RemoveColumns;
using Microsoft.Extensions.Logging;

namespace FilePrepper.Tests.Tasks;

/// <summary>
/// The output header was the first row's keys alone. <c>datetime</c> made its feature columns only for a row whose date it could
/// read, so a file whose first row had no date lost the year and month columns for every row — including the five that had one.
/// The header is every column any row has, and <c>datetime</c> gives every row every feature column.
/// </summary>
public class FirstRowColumnsTests : TaskBaseTest<DateTimeTask>
{
    public FirstRowColumnsTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void A_first_row_without_a_date_does_not_take_the_feature_columns_from_the_rest()
    {
        WriteTestFileLines("id,when", "1,", "2,2026-03-04", "3,not a date");
        var option = new DateTimeOption { InputPath = _testInputPath, OutputPath = _testOutputPath, Column = "when", Mode = DateTimeMode.ExtractFeatures, Features = "Year,Month" };

        Assert.True(new DateTimeTask(_mockLogger.Object).Execute(new TaskContext(option)));

        Assert.Equal(["id,when,when_Year,when_Month", "1,,,", "2,2026-03-04,2026,3", "3,not a date,,"], ReadOutputFileLines());
    }

    /// <summary>A task that gives a column to the second row only, as a task does for the rows it could read a value from.</summary>
    private sealed class GivesTheSecondRowAColumn(ILogger logger) : BaseTask<RemoveColumnsOption>(logger)
    {
        protected override Task<List<Dictionary<string, string>>> ProcessRecordsAsync(List<Dictionary<string, string>> records)
        {
            records[1]["extra"] = "x";
            return Task.FromResult(records);
        }
    }

    [Fact]
    public void The_output_has_every_column_any_row_has_in_the_order_they_first_appear()
    {
        WriteTestFileLines("a,b", "1,2", "3,4", "5,6");
        var option = new RemoveColumnsOption { InputPath = _testInputPath, OutputPath = _testOutputPath, RemoveColumns = ["b"] };

        Assert.True(new GivesTheSecondRowAColumn(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance).Execute(new TaskContext(option)));

        Assert.Equal(["a,b,extra", "1,2,", "3,4,x", "5,6,"], ReadOutputFileLines());
    }
}
