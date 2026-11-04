using System.Text;
using FilePrepper.Pipeline;
using FluentAssertions;

namespace FilePrepper.Tests.Utils;

/// <summary>
/// What a delimited file means when it is read and written — the contract the CSV library underneath must keep. The library's
/// own defaults changed between major versions (a malformed quote is recovered rather than refused, a one-cell empty record is
/// written as a quoted empty cell), and these pin the behaviour the tools here promise.
/// </summary>
public class DelimitedTableContractTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"fp-table-{Guid.NewGuid():N}");

    public DelimitedTableContractTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private string Write(string name, string text)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, text, new UTF8Encoding(false));
        return path;
    }

    [Theory]
    [InlineData("a,b\nab\"cd,2\n")]          // a quote inside an unquoted cell
    [InlineData("a,b\n\"abc,2\n3,4\n")]       // a quote that never closes: recovering would make the rest of the file one cell
    [InlineData("a,b\n\"x\"y,2\n")]           // text after a closing quote
    public async Task A_malformed_quote_is_refused_not_read_as_something_else(string text)
    {
        var path = Write("bad.csv", text);

        await Assert.ThrowsAnyAsync<FormatException>(() => DataPipeline.FromCsvAsync(path));
        await Assert.ThrowsAnyAsync<FormatException>(() => DataFrame.FromCsvAsync(path));
    }

    [Fact]
    public async Task A_short_row_reads_its_missing_cells_as_empty()
    {
        var path = Write("short.csv", "a,b,c\n1,2\n3,4,5\n");

        var frame = (await DataPipeline.FromCsvAsync(path)).ToDataFrame();

        frame.ColumnNames.Should().Equal("a", "b", "c");
        frame.Rows[0]["c"].Should().BeEmpty();
        frame.Rows[1]["c"].Should().Be("5");
    }

    [Fact]
    public async Task A_row_with_an_empty_cell_in_a_one_column_table_is_still_a_row_after_a_round_trip()
    {
        var source = Write("one.csv", "a\n1\n\"\"\n3\n");
        var written = Path.Combine(_dir, "one-out.csv");

        await (await DataPipeline.FromCsvAsync(source)).ToCsvAsync(written);

        // A blank line is skipped when a table is read, so the empty cell must be written as a quoted empty cell.
        File.ReadAllText(written).Should().Be("a\r\n1\r\n\"\"\r\n3\r\n");
        (await DataPipeline.FromCsvAsync(written)).ToDataFrame().RowCount.Should().Be(3);
    }

    [Fact]
    public async Task A_cell_that_looks_like_a_formula_is_written_as_it_was_read()
    {
        var source = Write("formula.csv", "a,b\n=1+1,-5\n@x,+3\n");
        var written = Path.Combine(_dir, "formula-out.csv");

        await (await DataPipeline.FromCsvAsync(source)).ToCsvAsync(written);

        File.ReadAllText(written).Should().Be("a,b\r\n=1+1,-5\r\n@x,+3\r\n");
    }

    [Fact]
    public async Task A_cell_with_a_leading_or_trailing_space_is_quoted_so_a_trimming_reader_keeps_it()
    {
        var source = Write("space.csv", "a,b\n\"  x  \",y\n");
        var written = Path.Combine(_dir, "space-out.csv");

        await (await DataPipeline.FromCsvAsync(source)).ToCsvAsync(written);

        File.ReadAllText(written).Should().Be("a,b\r\n\"  x  \",y\r\n");
    }

    [Fact]
    public async Task A_file_said_to_have_no_header_is_not_read_by_the_pipeline_as_an_empty_table()
    {
        var path = Write("noheader.csv", "1,2\n3,4\n");

        await Assert.ThrowsAsync<NotSupportedException>(() => DataPipeline.FromCsvAsync(path, hasHeader: false));
    }
}
