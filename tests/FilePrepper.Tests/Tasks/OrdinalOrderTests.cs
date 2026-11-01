using FilePrepper.Pipeline;
using FilePrepper.Tasks;
using FilePrepper.Tasks.OneHotEncoding;
using FilePrepper.Tasks.WindowOps;

namespace FilePrepper.Tests.Tasks;

/// <summary>
/// Output that is ordered by text is ordered ordinally, the same on every machine: the default comparer is the current
/// culture's, which puts <c>a</c> before <c>B</c> where an ordinal sort puts <c>B</c> before <c>a</c>.
/// </summary>
public class OrdinalOrderTests : TaskBaseTest<OneHotEncodingTask>
{
    public OrdinalOrderTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void One_hot_columns_come_out_in_ordinal_order()
    {
        WriteTestFileLines("Id,Tag", "1,a", "2,B", "3,c", "4,A");
        var option = new OneHotEncodingOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            TargetColumns = ["Tag"],
            DropFirst = false,
            KeepOriginalColumns = false
        };

        Assert.True(new OneHotEncodingTask(_mockLogger.Object).Execute(new TaskContext(option)));

        Assert.Equal("Id,Tag_A,Tag_B,Tag_a,Tag_c", ReadOutputFileLines()[0]);
    }

    [Fact]
    public void Grouped_rows_come_out_in_ordinal_key_order()
    {
        var data = DataPipeline.FromData(new[]
        {
            new Dictionary<string, string> { ["k"] = "b", ["v"] = "1" },
            new Dictionary<string, string> { ["k"] = "A", ["v"] = "2" },
            new Dictionary<string, string> { ["k"] = "a", ["v"] = "3" },
            new Dictionary<string, string> { ["k"] = "B", ["v"] = "4" },
        });

        var keys = data.GroupBy("k").Aggregate(new[] { ("v", AggregationMethod.Sum) }).ToDataFrame().GetColumn("k").ToList();

        Assert.Equal(["A", "B", "a", "b"], keys);
    }
}
