using FilePrepper.Tasks;
using FilePrepper.Tasks.RemoveConstants;

namespace FilePrepper.Tests.Tasks;

public class RemoveConstantsTests : TaskBaseTest<RemoveConstantsTask>
{
    public RemoveConstantsTests(ITestOutputHelper output) : base(output)
    {
    }

    private bool Run(double threshold = 0.0, bool reportOnly = false)
    {
        var option = new RemoveConstantsOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            UniqueRatioThreshold = threshold,
            ReportOnly = reportOnly
        };
        return new RemoveConstantsTask(_mockLogger.Object).Execute(new TaskContext(option));
    }

    [Fact]
    public void Exact_constants_are_removed_and_an_empty_cell_is_a_value_like_any_other()
    {
        // "const" is the same in every row; "blank" is empty in every row (constant); "mix" has one
        // value and one empty cell (two values, kept).
        WriteTestFileLines(
            "id,const,blank,mix",
            "1,x,,a",
            "2,x,,",
            "3,x,,a");

        Assert.True(Run());

        var lines = ReadOutputFileLines();
        Assert.Equal(["id,mix", "1,a", "2,", "3,a"], lines);
    }

    [Fact]
    public void Values_are_compared_exactly_as_text()
    {
        // 1 and 1.0 are different text; a and A are different text — neither column is constant.
        WriteTestFileLines(
            "n,s",
            "1,a",
            "1.0,A");

        Assert.True(Run());

        Assert.Equal(["n,s", "1,a", "1.0,A"], ReadOutputFileLines());
    }

    [Fact]
    public void The_threshold_is_inclusive_distinct_values_over_rows()
    {
        // 10 rows, 2 distinct values in "two": ratio 0.2.
        var rows = Enumerable.Range(1, 10).Select(i => $"{i},{(i <= 5 ? "p" : "q")}").ToArray();
        WriteTestFileLines(["id,two", .. rows]);

        // Just below the ratio: kept.
        Assert.True(Run(threshold: 0.19));
        Assert.Equal("id,two", ReadOutputFileLines()[0]);

        // Exactly the ratio: removed ("at or below").
        Assert.True(Run(threshold: 0.2));
        Assert.Equal("id", ReadOutputFileLines()[0]);
    }

    [Fact]
    public void A_category_of_few_levels_over_many_rows_counts_as_low_variety()
    {
        // The ratio measures how many different values a column has, not how much one dominates:
        // three evenly spread levels over twelve rows is 25 %, and goes at a threshold of 0.25.
        var levels = new[] { "r", "g", "b" };
        var rows = Enumerable.Range(1, 12).Select(i => $"{i},{levels[i % 3]}").ToArray();
        WriteTestFileLines(["id,color", .. rows]);

        Assert.True(Run(threshold: 0.25));

        Assert.Equal("id", ReadOutputFileLines()[0]);
    }

    [Fact]
    public void Report_only_removes_nothing()
    {
        WriteTestFileLines(
            "id,const",
            "1,x",
            "2,x");

        Assert.True(Run(reportOnly: true));

        Assert.Equal(["id,const", "1,x", "2,x"], ReadOutputFileLines());
    }

    [Fact]
    public void Removing_every_column_is_refused_instead_of_writing_a_placeholder_table()
    {
        // One data row: every column has exactly one value. The output used to be a "NoData" header over
        // a blank line, written as if it had worked.
        WriteTestFileLines(
            "a,b,c",
            "1,2,3");

        var error = Assert.Throws<InvalidOperationException>(() => Run());

        Assert.Contains("All 3 column(s)", error.Message);
        Assert.Equal(0, new FileInfo(_testOutputPath).Length);
    }

    [Fact]
    public void Report_only_may_list_every_column_even_when_all_are_constant()
    {
        WriteTestFileLines(
            "a,b",
            "1,2");

        Assert.True(Run(reportOnly: true));

        Assert.Equal(["a,b", "1,2"], ReadOutputFileLines());
    }

    [Fact]
    public void A_file_with_a_header_and_no_rows_keeps_its_header()
    {
        WriteTestFileLines("a,b,c");

        Assert.True(Run());

        Assert.Equal(["a,b,c"], ReadOutputFileLines());
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(-0.1)]
    [InlineData(1.5)]
    public void A_threshold_outside_zero_to_one_is_invalid(double threshold)
    {
        var option = new RemoveConstantsOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            UniqueRatioThreshold = threshold
        };

        Assert.Contains(option.Validate(), e => e.Contains("between 0.0 and 1.0"));
    }
}
