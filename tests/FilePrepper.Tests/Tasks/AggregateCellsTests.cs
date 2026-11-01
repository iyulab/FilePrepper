using FilePrepper.Tasks;
using FilePrepper.Tasks.Aggregate;
using Microsoft.Extensions.Logging;
using Moq;

namespace FilePrepper.Tests.Tasks;

/// <summary>
/// Two ways <c>aggregate</c> answered wrongly without a word. It read <c>1,5</c> as 15 and left a cell like <c>x</c> out of the sum
/// with nothing said (the other numeric commands say which cells they left out); and it joined a row's group values with <c>|</c> and
/// split them again, so a value holding a <c>|</c> broke its group and ("a|b","c") was the group of ("a","b|c").
/// </summary>
public class AggregateCellsTests : TaskBaseTest<AggregateTask>
{
    public AggregateCellsTests(ITestOutputHelper output) : base(output)
    {
    }

    private string[] Run(string[] groupBy, string[] lines)
    {
        WriteTestFileLines(lines);
        var options = new AggregateOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            GroupByColumns = groupBy,
            AggregateColumns = [new AggregateColumn { ColumnName = "v", Function = AggregateFunction.Sum, OutputColumnName = "total" }],
        };

        Assert.True(new AggregateTask(_mockLogger.Object).Execute(new TaskContext(options)));
        return ReadOutputFileLines();
    }

    [Fact]
    public void A_cell_that_is_not_a_number_is_left_out_and_named_in_a_warning()
    {
        var lines = Run(["g"], ["g,v", "a,1", "a,\"1,5\"", "a,x", "a,", "b,4"]);

        Assert.Equal(["g,total", "a,1", "b,4"], lines);
        _mockLogger.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => $"{state}".Contains("2 cell(s) of column 'v' are not numbers", StringComparison.Ordinal) && $"{state}".Contains("first: '1,5'", StringComparison.Ordinal)),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public void A_group_value_holding_the_old_separator_stays_one_group_and_apart_from_its_lookalike()
    {
        var lines = Run(["g", "h"], ["g,h,v", "a|b,c,1", "a|b,c,2", "a,b|c,10", "a,b|c,20"]);

        Assert.Equal(["g,h,total", "a|b,c,3", "a,b|c,30"], lines);
    }
}
