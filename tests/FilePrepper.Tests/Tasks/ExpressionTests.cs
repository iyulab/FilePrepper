using FilePrepper.Tasks;
using FilePrepper.Tasks.Expression;

namespace FilePrepper.Tests.Tasks;

/// <summary>
/// <see cref="ExpressionTask"/> had no test. Reading it to document its options found a cell such as 0.00001 that
/// broke the expression (it was written into it as <c>1E-05</c>), a position the task accepted and ignored, a cell
/// <c>1,5</c> read as 15, an empty cell read as 0, and a check for mistyped column names that could never fire.
/// </summary>
public class ExpressionTests : TaskBaseTest<ExpressionTask>
{
    public ExpressionTests(ITestOutputHelper output) : base(output)
    {
    }

    private string[] Run(string[] lines, bool removeSource, params string[] expressions)
    {
        WriteTestFileLines(lines);
        var option = new ExpressionOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            RemoveSourceColumns = removeSource,
            Expressions = [.. expressions.Select(ColumnExpression.Parse)]
        };
        Assert.True(new ExpressionTask(_mockLogger.Object).Execute(new TaskContext(option)));
        return ReadOutputFileLines();
    }

    private string[] Run(string[] lines, params string[] expressions) => Run(lines, false, expressions);

    [Theory]
    [InlineData("0.00001", "2", "a*b", "2E-05")]
    [InlineData("0.00001", "2", "a+b", "2.00001")]
    [InlineData("1E-05", "2", "a*b", "2E-05")]
    [InlineData("1000000000000000", "3", "a*b", "3000000000000000")]
    [InlineData("1E+20", "1", "a-b", "1E+20")]
    public void A_cell_is_the_number_it_is_however_it_prints(string a, string b, string expression, string expected)
    {
        var lines = Run(["a,b", $"{a},{b}"], $"s={expression}");

        Assert.Equal($"{a},{b},{expected}", lines[1]);
    }

    [Theory]
    [InlineData("(a+b)*(a-b)", "5")]
    [InlineData("a*-b", "-6")]
    [InlineData("-a+b", "-1")]
    [InlineData("2*(3+4)-a", "11")]
    [InlineData("a - -b", "5")]
    [InlineData("10/a/b", "1.6666666666666667")]
    [InlineData("1e-1*a", "0.30000000000000004")]
    [InlineData("a+2.5E+1", "28")]
    public void Arithmetic_follows_the_usual_rules(string expression, string expected)
    {
        var lines = Run(["a,b", "3,2"], $"s={expression}");

        Assert.EndsWith("," + expected, lines[1]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("N/A")]
    [InlineData("1,5")]
    [InlineData("$5")]
    public void A_cell_that_is_empty_or_not_a_number_makes_the_result_empty_not_a_computation_on_zero(string cell)
    {
        // It was read as 0 - or, for 1,5 and $5, as 15 and 5 - so a sum over a missing price was a total of what was left.
        var lines = Run(["id,a,b", $"1,{(cell.Contains(',') ? $"\"{cell}\"" : cell)},2", "2,10,2"], "s=a+b");

        Assert.EndsWith(",2,", lines[1]);
        Assert.EndsWith(",10,2,12", lines[2]);
    }

    [Fact]
    public void A_mistyped_column_is_named_before_any_record_is_computed()
    {
        WriteTestFileLines("a,b", "1,2");
        var option = new ExpressionOption
        {
            InputPath = _testInputPath,
            OutputPath = _testOutputPath,
            Expressions = [ColumnExpression.Parse("s=a+bb*cc")]
        };

        var ex = Assert.ThrowsAny<Exception>(() => new ExpressionTask(_mockLogger.Object).Execute(new TaskContext(option)));

        Assert.Contains("non-existent columns: bb, cc", ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_column_name_may_hold_spaces_and_punctuation_and_the_longest_name_wins()
    {
        var lines = Run(["unit price,price($),a,ab", "4,3,10,100"], "s=unit price*price($)+a+ab");

        Assert.EndsWith(",122", lines[1]);
    }

    [Theory]
    [InlineData("s=a+b@0", "s,a,b,c")]
    [InlineData("s=a+b@1", "a,s,b,c")]
    [InlineData("s=a+b@3", "a,b,c,s")]
    [InlineData("s=a+b@99", "a,b,c,s")]
    [InlineData("s=a+b", "a,b,c,s")]
    public void A_computed_column_goes_where_its_position_says(string expression, string header)
    {
        var lines = Run(["a,b,c", "1,2,3"], expression);

        Assert.Equal(header, lines[0]);
    }

    [Fact]
    public void A_computed_column_named_like_a_source_column_replaces_it()
    {
        var lines = Run(["a,b", "1,2"], "a=a+b");

        Assert.Equal("b,a", lines[0]);
        Assert.Equal("2,3", lines[1]);
    }

    [Fact]
    public void Source_columns_the_expressions_read_can_be_dropped()
    {
        var lines = Run(["id,a,b,keep", "1,2,3,x"], true, "s=a+b");

        Assert.Equal("id,keep,s", lines[0]);
        Assert.Equal("1,x,5", lines[1]);
    }

    [Fact]
    public void Dividing_by_zero_is_an_error_but_a_tiny_divisor_is_a_divisor()
    {
        Assert.EndsWith(",1000000000000", Run(["a,b", "1,0.000000000001"], "s=a/b")[1]);

        WriteTestFileLines("a,b", "1,0");
        var option = new ExpressionOption { InputPath = _testInputPath, OutputPath = _testOutputPath, Expressions = [ColumnExpression.Parse("s=a/b")] };
        var ex = Assert.ThrowsAny<Exception>(() => new ExpressionTask(_mockLogger.Object).Execute(new TaskContext(option)));
        Assert.Contains("Division by zero", ex.ToString(), StringComparison.Ordinal);
    }
}
