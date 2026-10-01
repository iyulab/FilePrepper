using FilePrepper.Pipeline;
using FilePrepper.Utils;
using FluentAssertions;

namespace FilePrepper.Tests.Utils;

/// <summary>
/// SVMlight / libsvm text (<c>label [qid:q] index:value …</c>) is read as a table: one row per
/// line, a <c>label</c> column, a <c>qid</c> column when the file groups its rows, and one
/// <c>f&lt;index&gt;</c> column per feature index that occurs.
/// </summary>
public class SvmLightInputTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"fp-svm-{Guid.NewGuid():N}");

    public SvmLightInputTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private string Write(string name, params string[] lines)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllLines(path, lines);
        return path;
    }

    [Fact]
    public async Task Each_line_is_a_row_and_an_absent_feature_is_zero()
    {
        var path = Write("rank.svm",
            "2 qid:7 1:0.5 3:1.25 # a comment",
            "0 qid:7 2:4",
            "1 qid:9 1:-1 3:2e-3");

        var (records, headers) = await SvmLightUtils.ReadSvmLightFileAsync(path);

        headers.Should().Equal("label", "qid", "f1", "f2", "f3");
        records.Should().HaveCount(3);
        records[0].Should().Contain(new Dictionary<string, string>
        {
            ["label"] = "2", ["qid"] = "7", ["f1"] = "0.5", ["f2"] = "0", ["f3"] = "1.25"
        });
        records[2]["f3"].Should().Be("2e-3");
    }

    [Fact]
    public async Task Without_qid_a_group_size_file_beside_it_supplies_the_groups()
    {
        // LightGBM's and XGBoost's convention: rank.train holds the rows, rank.train.query the
        // number of rows in each consecutive group.
        var path = Write("rank.train", "1 1:1", "0 1:2", "2 1:3", "0 1:4", "1 1:5");
        Write("rank.train.query", "2", "3");

        var (records, headers) = await SvmLightUtils.ReadSvmLightFileAsync(path);

        headers.Should().Equal("label", "qid", "f1");
        records.Select(r => r["qid"]).Should().Equal("0", "0", "1", "1", "1");
    }

    [Fact]
    public async Task A_group_size_file_that_does_not_cover_the_rows_is_refused()
    {
        var path = Write("rank.train", "1 1:1", "0 1:2", "2 1:3");
        Write("rank.train.query", "2", "2");

        var act = () => SvmLightUtils.ReadSvmLightFileAsync(path);

        (await act.Should().ThrowAsync<InvalidDataException>())
            .Which.Message.Should().Contain("4").And.Contain("3");
    }

    [Fact]
    public async Task Without_qid_or_a_group_file_there_is_no_qid_column()
    {
        var path = Write("plain.libsvm", "+1 1:1 4:2", "-1 2:1");

        var (records, headers) = await SvmLightUtils.ReadSvmLightFileAsync(path);

        headers.Should().Equal("label", "f1", "f2", "f4");
        records[0]["label"].Should().Be("+1");
    }

    [Theory]
    [InlineData("data.svm")]
    [InlineData("data.svmlight")]
    [InlineData("data.LIBSVM")]
    public void The_format_is_named_by_its_extensions(string name) =>
        DataFileFormats.FromPath(name).Should().Be(DataFileFormat.SvmLight);

    [Fact]
    public void A_file_whose_extension_names_no_format_is_recognized_by_its_lines()
    {
        // rank.train: the extension says nothing, and read as CSV it was one column whose header
        // was the first data line.
        var path = Write("rank.train", "# header comment", "0 10:0.89 11:0.75", "1 qid:3 2:1");

        DataFileFormats.FromPath(path).Should().Be(DataFileFormat.SvmLight);
        DataFileFormats.IsReadable(path).Should().BeTrue();
    }

    [Theory]
    [InlineData("a,b,c", "1,2,3")]
    [InlineData("value", "12")]
    [InlineData("1 10:30 meeting", "2 11:00 lunch")]
    public void Delimited_text_is_not_mistaken_for_it(params string[] lines)
    {
        var path = Write("data.txt", lines);

        DataFileFormats.FromPath(path).Should().Be(DataFileFormat.Csv);
    }

    [Fact]
    public async Task The_pipeline_reads_it_by_the_same_decision()
    {
        var path = Write("rank.train", "1 1:1 2:0.5", "0 2:3");

        var pipeline = await DataPipeline.FromFileAsync(path);

        pipeline.ColumnNames.Should().Equal("label", "f1", "f2");
    }
}
