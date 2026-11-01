using System.Text;
using FilePrepper.Pipeline;
using FilePrepper.Utils;
using FluentAssertions;

namespace FilePrepper.Tests.Utils;

/// <summary>
/// A delimited file not named <c>.tsv</c> is read with the delimiter its own first lines prove. Excel in many locales writes
/// <c>;</c> under the name <c>.csv</c>; read as a comma table such a file is one column headed by its whole first line.
/// </summary>
public class DelimiterSniffingTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"fp-delim-{Guid.NewGuid():N}");

    public DelimiterSniffingTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private string Write(string name, string text, Encoding? encoding = null)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, text, encoding ?? new UTF8Encoding(false));
        return path;
    }

    [Theory]
    [InlineData("a;b;c\n1;2;3\n4;5;6\n", ";")]
    [InlineData("a\tb\tc\n1\t2\t3\n4\t5\t6\n", "\t")]
    [InlineData("a|b|c\n1|2|3\n4|5|6\n", "|")]
    [InlineData("a,b,c\n1,2,3\n4,5,6\n", ",")]
    public void A_file_is_read_with_the_delimiter_its_lines_prove(string text, string expected)
    {
        DataFileFormats.DelimiterFor(Write("t.csv", text)).Should().Be(expected);
    }

    [Fact]
    public async Task A_semicolon_file_reads_as_its_columns_with_decimal_commas_kept_as_text()
    {
        var path = Write("eu.csv", "id;price;지역\r\n1;1,5;서울\r\n2;2,25;부산\r\n");

        var pipeline = await DataPipeline.FromFileAsync(path);
        var frame = pipeline.ToDataFrame();

        frame.ColumnNames.Should().Equal("id", "price", "지역");
        frame.RowCount.Should().Be(2);
        frame.GetColumn("price").Should().Equal("1,5", "2,25");
    }

    [Fact]
    public void A_byte_order_mark_and_blank_lines_do_not_hide_the_delimiter()
    {
        var path = Write("bom.csv", "a;b\n1;2\n\n3;4\n\n\n", new UTF8Encoding(true));

        DataFileFormats.DelimiterFor(path).Should().Be(";");
    }

    [Fact]
    public void A_comma_table_whose_cells_hold_a_semicolon_is_still_a_comma_table()
    {
        DataFileFormats.DelimiterFor(Write("c.csv", "a,b\nx;y,1\nx;y;z,2\n")).Should().Be(",");
    }

    [Fact]
    public void A_semicolon_inside_quotes_is_not_a_delimiter()
    {
        // The header is a comma header; the quoted semicolons are cell text.
        DataFileFormats.DelimiterFor(Write("q.csv", "a,b\n\"x;y\",1\n\"p;q;r\",2\n")).Should().Be(",");
        // One quoted column name with a semicolon is still one column: only one line fits, the rest do not.
        DataFileFormats.DelimiterFor(Write("q1.csv", "\"a;b\"\nx\ny\n")).Should().Be(",");
    }

    [Fact]
    public void Lines_that_disagree_on_the_count_prove_nothing()
    {
        DataFileFormats.DelimiterFor(Write("ragged.csv", "a;b;c\n1;2;3\n4;5\n")).Should().Be(",");
    }

    [Fact]
    public void Two_delimiters_that_both_fit_are_not_told_apart()
    {
        DataFileFormats.DelimiterFor(Write("both.csv", "a;b|c\n1;2|3\n4;5|6\n")).Should().Be(",");
    }

    [Fact]
    public void A_header_only_file_and_an_empty_file_and_a_missing_file_stay_comma_tables()
    {
        DataFileFormats.DelimiterFor(Write("h.csv", "a;b;c\n")).Should().Be(";");
        DataFileFormats.DelimiterFor(Write("e.csv", "")).Should().Be(",");
        DataFileFormats.DelimiterFor(Path.Combine(_dir, "nope.csv")).Should().Be(",");
    }

    [Fact]
    public void A_tsv_is_a_tab_file_whatever_its_lines_say()
    {
        DataFileFormats.DelimiterFor(Write("t.tsv", "a;b\n1;2\n")).Should().Be("\t");
    }

    [Fact]
    public async Task A_semicolon_file_written_out_as_csv_is_a_comma_table_of_the_same_cells()
    {
        // The seam MLoop uses: whatever the delimiter, convert once to a comma CSV and everything after reads that.
        var path = Write("eu2.csv", "a;b\n1;\"x;y\"\n");
        var output = Path.Combine(_dir, "out.csv");

        await (await DataPipeline.FromFileAsync(path)).ToCsvAsync(output);

        DataFileFormats.DelimiterFor(output).Should().Be(",");
        File.ReadAllText(output).Replace("\r\n", "\n").Should().Be("a,b\n1,x;y\n");
    }
}
