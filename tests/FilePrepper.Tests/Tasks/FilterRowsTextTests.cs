using System.Globalization;
using FilePrepper.Tasks;
using FilePrepper.Tasks.FilterRows;
using FilePrepper.Tests.Culture;

namespace FilePrepper.Tests.Tasks;

/// <summary>
/// The four text operators of <see cref="FilterRowsTask"/> follow one rule — ordinal, ignoring case, the same on every
/// machine. <c>NotContains</c> used to compare case-sensitively (a row could pass both <c>Contains</c> and
/// <c>NotContains</c>), and <c>StartsWith</c>/<c>EndsWith</c> compared in the machine's culture.
/// </summary>
[Collection(nameof(CurrentCultureCollection))]
public class FilterRowsTextTests : TaskBaseTest<FilterRowsTask>
{
    public FilterRowsTextTests(ITestOutputHelper output) : base(output)
    {
    }

    private string[] Kept(FilterOperator op, string value)
    {
        WriteTestFileLines("name", "Alice", "ALBERT", "bob", "Istanbul", "x-ray");
        var option = new FilterRowsOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Conditions = [new FilterCondition { ColumnName = "name", Operator = op, Value = value }]
        };
        Assert.True(new FilterRowsTask(_mockLogger.Object).Execute(new TaskContext(option)));
        return ReadOutputFileLines().Skip(1).ToArray();
    }

    [Theory]
    [InlineData("al")]
    [InlineData("AL")]
    [InlineData("b")]
    [InlineData("zzz")]
    public void NotContains_keeps_exactly_the_rows_Contains_leaves_out(string value)
    {
        var contains = Kept(FilterOperator.Contains, value);
        var notContains = Kept(FilterOperator.NotContains, value);

        Assert.Empty(contains.Intersect(notContains));
        Assert.Equal(5, contains.Length + notContains.Length);
    }

    [Fact]
    public void StartsWith_and_EndsWith_ignore_case_like_Contains()
    {
        Assert.Equal(["Alice", "ALBERT"], Kept(FilterOperator.StartsWith, "al"));
        Assert.Equal(["Alice"], Kept(FilterOperator.EndsWith, "ICE"));
    }

    [Fact]
    public void The_text_operators_give_the_same_rows_on_a_turkish_machine()
    {
        // In tr-TR a culture comparison treats I and i as different letters, so "Istanbul" did not start with "i".
        var saved = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
        try
        {
            Assert.Equal(["Istanbul"], Kept(FilterOperator.StartsWith, "i"));
            Assert.Equal(["Istanbul"], Kept(FilterOperator.Contains, "ISTAN"));
            Assert.Equal(["Istanbul"], Kept(FilterOperator.StartsWith, "I"));
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Fact]
    public void A_value_the_culture_would_ignore_is_still_a_value()
    {
        // A soft hyphen is ignorable in a culture comparison, so every row "started with" it.
        Assert.Empty(Kept(FilterOperator.StartsWith, "­"));
    }
}
