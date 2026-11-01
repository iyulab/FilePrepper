using FilePrepper.Tasks;
using FilePrepper.Tasks.Conditional;

namespace FilePrepper.Tests.Tasks;

/// <summary>
/// <see cref="ConditionalTask"/> had no test. Reading it to document its options found an ordering comparison that
/// called a cell that is not a number greater than 100 (and an empty one less than 100), and a split at the first
/// colon that made a time such as <c>12:30</c> impossible to compare with.
/// </summary>
public class ConditionalTests : TaskBaseTest<ConditionalTask>
{
    public ConditionalTests(ITestOutputHelper output) : base(output)
    {
    }

    // input[0] is the column's name, the rest its cells; an id column goes in front so that an empty cell is still a row.
    private string[] Run(string[] input, string elseValue, params string[] conditions)
    {
        WriteTestFileLines(["id," + input[0], .. input.Skip(1).Select((cell, i) => $"{i},{cell}")]);
        var option = new ConditionalOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            OutputColumn = "out",
            ElseValue = elseValue,
            Conditions = [.. conditions]
        };
        Assert.True(new ConditionalTask(_mockLogger.Object).Execute(new TaskContext(option)));
        return ReadOutputFileLines().Skip(1).Select(l => l[(l.LastIndexOf(',') + 1)..]).ToArray();
    }

    [Fact]
    public void A_cell_that_is_not_a_number_does_not_pass_a_numeric_ordering_test()
    {
        var input = new[] { "price", "150", "abc", "", "50", "N/A" };

        Assert.Equal(["High", "Low", "Low", "Low", "Low"], Run(input, "Low", "price > 100 : High"));
    }

    [Fact]
    public void An_empty_cell_is_not_cheap()
    {
        // It used to compare as text: "" sorts before "100", so a missing price was labelled cheap.
        var rows = Run(["price", "50", "", "150"], "other", "price < 100 : cheap");

        Assert.Equal(["cheap", "other", "other"], rows);
    }

    [Fact]
    public void All_four_ordering_operators_agree_on_a_cell_that_is_not_a_number()
    {
        foreach (var op in new[] { ">", "<", ">=", "<=" })
        {
            Assert.Equal(["no"], Run(["v", "abc"], "no", $"v {op} 5 : yes"));
        }
    }

    [Theory]
    [InlineData(">=", "5", "yes")]
    [InlineData(">=", "4.5", "no")]
    [InlineData("<=", "5", "yes")]
    [InlineData("<=", "5.5", "no")]
    public void Numbers_compare_as_numbers(string op, string cell, string expected)
    {
        Assert.Equal([expected], Run(["v", cell], "no", $"v {op} 5 : yes"));
    }

    [Fact]
    public void A_value_that_is_not_a_number_still_compares_as_text()
    {
        // ISO dates order as text.
        Assert.Equal(["late", "early"], Run(["d", "2026-03-01", "2025-12-31"], "early", "d > 2026-01-01 : late"));
    }

    [Fact]
    public void The_value_may_hold_a_colon_when_the_result_is_set_off_by_spaces()
    {
        var rows = Run(["t", "12:30", "09:00"], "off", "t == 12:30 : peak");

        Assert.Equal(["peak", "off"], rows);
    }

    [Fact]
    public void A_line_without_spaces_around_the_colon_splits_at_the_first_colon()
    {
        Assert.Equal(["High", "Low"], Run(["price", "150", "50"], "Low", "price>100:High"));
    }

    [Fact]
    public void Validation_and_parsing_split_a_line_the_same_way()
    {
        var option = new ConditionalOption { OutputColumn = "o", Conditions = ["t == 12:30 : peak"], InputPath = _testInputPath, OutputPath = _testOutputPath };

        Assert.Empty(option.Validate());
        var parsed = ConditionalExpression.Parse("t == 12:30 : peak");
        Assert.Equal(("t", "12:30", "peak"), (parsed.ColumnName, parsed.CompareValue, parsed.ResultValue));
    }

    [Fact]
    public void Equality_and_the_word_operators_ignore_case()
    {
        Assert.Equal(["yes", "yes", "no"], Run(["status", "Active", "ACTIVE", "inactive"], "no", "status == active : yes"));
        Assert.Equal(["yes", "yes", "no"], Run(["status", "Active", "ACTIVE", "done"], "no", "status contains act : yes"));
    }

    [Fact]
    public void The_first_condition_a_record_meets_wins()
    {
        Assert.Equal(["A", "B", "C"], Run(["v", "9", "5", "1"], "C", "v > 8 : A", "v > 3 : B"));
    }
}
