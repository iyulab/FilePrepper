using System.Globalization;
using FilePrepper.Tasks;
using FilePrepper.Tasks.StringOps;
using FilePrepper.Tests.Culture;

namespace FilePrepper.Tests.Tasks;

/// <summary>
/// <see cref="StringTask"/> — no test covered it before, and reading it to document its options found four
/// disagreements between what an option promises and what the task does.
/// </summary>
[Collection(nameof(CurrentCultureCollection))]
public class StringTests : TaskBaseTest<StringTask>
{
    public StringTests(ITestOutputHelper output) : base(output)
    {
    }

    private string[] Run(StringOption option)
    {
        option.InputPath = _testInputPath;
        option.OutputPath = _testOutputPath;
        Assert.True(new StringTask(_mockLogger.Object).Execute(new TaskContext(option)));
        return ReadOutputFileLines();
    }

    [Fact]
    public void Replace_accepts_whitespace_as_the_text_to_look_for()
    {
        // " " is the commonest thing to replace, and "required" means present — not "not blank".
        var option = new StringOption { Mode = StringMode.Replace, Column = "name", OldValue = " ", NewValue = "_" };

        Assert.DoesNotContain(option.Validate(), e => e.Contains("OldValue"));

        WriteTestFileLines("name,n", "title one,1", "a b c,2");
        var lines = Run(option);

        Assert.Equal("title_one,1", lines[1]);
        Assert.Equal("a_b_c,2", lines[2]);
    }

    [Fact]
    public void Replace_still_refuses_an_empty_text_to_look_for()
    {
        // The empty string matches between every two characters; string.Replace itself throws on it.
        var option = new StringOption { Mode = StringMode.Replace, Column = "name", OldValue = "" };

        Assert.Contains(option.Validate(), e => e.Contains("OldValue"));
    }

    [Fact]
    public void Substring_refuses_a_negative_length()
    {
        // It used to fill the column with empty strings, with no word about why.
        var option = new StringOption { Mode = StringMode.Substring, Column = "name", Length = -3 };

        Assert.Contains(option.Validate(), e => e.Contains("Length"));
    }

    [Theory]
    [InlineData(0, 4, "titl")]
    [InlineData(2, null, "tle one")]
    [InlineData(6, 50, "one")]
    [InlineData(20, null, "")]
    [InlineData(0, 0, "")]
    public void Substring_takes_what_there_is(int start, int? length, string expected)
    {
        WriteTestFileLines("name", "title one");

        var lines = Run(new StringOption { Mode = StringMode.Substring, Column = "name", StartIndex = start, Length = length, OutputColumn = "out" });

        Assert.Equal("title one," + expected, lines[1]);
    }

    [Fact]
    public void Upper_and_lower_give_the_same_letters_in_every_culture()
    {
        // On tr-TR "title".ToUpper() is "TİTLE" (dotted capital I), and "TITLE".ToLower() is "tıtle".
        var saved = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
        try
        {
            WriteTestFileLines("name", "title", "TITLE");

            var upper = Run(new StringOption { Mode = StringMode.Upper, Column = "name" });
            Assert.Equal("TITLE", upper[1]);

            WriteTestFileLines("name", "title", "TITLE");
            var lower = Run(new StringOption { Mode = StringMode.Lower, Column = "name" });
            Assert.Equal("title", lower[2]);
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Theory]
    [InlineData(TrimMode.Both, "x")]
    [InlineData(TrimMode.Start, "x  ")]
    [InlineData(TrimMode.End, "  x")]
    public void Trim_strips_the_chosen_end(TrimMode mode, string expected)
    {
        WriteTestFileLines("v", "\"  x  \"");

        var lines = Run(new StringOption { Mode = StringMode.Trim, Column = "v", TrimMode = mode });

        Assert.Equal(expected.Contains(' ') ? $"\"{expected}\"" : expected, lines[1]);
    }

    [Fact]
    public void Concat_joins_in_the_order_given()
    {
        WriteTestFileLines("a,b", "1,2");

        var lines = Run(new StringOption { Mode = StringMode.Concat, Columns = "b, a", Separator = "-", OutputColumn = "ab" });

        Assert.Equal("1,2,2-1", lines[1]);
    }

    [Fact]
    public void Concat_refuses_a_column_that_is_not_in_the_file_instead_of_joining_an_empty_piece()
    {
        // "b, a, nope" used to write "2-1-": the missing column contributed an empty piece and its separator.
        WriteTestFileLines("a,b", "1,2");
        var option = new StringOption { Mode = StringMode.Concat, Columns = "b, a, nope", Separator = "-", OutputColumn = "ab", InputPath = _testInputPath, OutputPath = _testOutputPath };

        var error = Assert.Throws<ValidationException>(() => new StringTask(_mockLogger.Object).Execute(new TaskContext(option)));

        Assert.Contains("Column 'nope' not found", error.Message);
    }
}
