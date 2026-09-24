using FilePrepper.Pipeline;
using FilePrepper.Utils;
using FluentAssertions;
using Parquet.Serialization;

namespace FilePrepper.Tests.Pipeline;

public class ParquetInputTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"fp-parquet-{Guid.NewGuid():N}");

    public ParquetInputTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    public sealed class Labels
    {
        public double Label { get; set; }
        public long BinaryLabel { get; set; }
    }

    public sealed class Pair
    {
        public string Sentence1 { get; set; } = "";
        public string? Sentence2 { get; set; }
        public Labels Labels { get; set; } = new();
        public List<int> Tags { get; set; } = [];
    }

    private async Task<string> WriteAsync(IEnumerable<Pair> rows)
    {
        var path = Path.Combine(_dir, "pairs.parquet");
        await ParquetSerializer.SerializeAsync(rows, path);
        return path;
    }

    private static Pair[] Sample() =>
    [
        new() { Sentence1 = "숙소가 깨끗합니다.", Sentence2 = "숙소는 청결했습니다.", Labels = new() { Label = 3.7, BinaryLabel = 1 }, Tags = [1, 2] },
        new() { Sentence1 = "위치가 좋아요.", Sentence2 = null, Labels = new() { Label = 0.25, BinaryLabel = 0 }, Tags = [] }
    ];

    [Fact]
    public async Task A_struct_becomes_one_column_per_leaf_named_by_its_path()
    {
        var (records, headers) = await ParquetUtils.ReadParquetFileAsync(await WriteAsync(Sample()));

        headers.Should().Equal("Sentence1", "Sentence2", "Labels.Label", "Labels.BinaryLabel", "Tags");
        records.Should().HaveCount(2);
        records[0]["Labels.Label"].Should().Be("3.7");
        records[0]["Labels.BinaryLabel"].Should().Be("1");
        records[0]["Sentence1"].Should().Be("숙소가 깨끗합니다.");
    }

    [Fact]
    public async Task A_list_stays_one_column_as_json_and_a_null_is_an_empty_cell()
    {
        var (records, _) = await ParquetUtils.ReadParquetFileAsync(await WriteAsync(Sample()));

        records[0]["Tags"].Should().Be("[1,2]");
        records[1]["Tags"].Should().Be("[]");
        records[1]["Sentence2"].Should().BeEmpty();
    }

    [Fact]
    public async Task Numbers_are_written_in_the_invariant_culture()
    {
        var saved = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            var (records, _) = await ParquetUtils.ReadParquetFileAsync(await WriteAsync(Sample()));
            records[1]["Labels.Label"].Should().Be("0.25");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = saved;
        }
    }

    [Fact]
    public async Task FromFileAsync_picks_the_parquet_reader_by_extension_and_writes_csv()
    {
        var path = await WriteAsync(Sample());
        var csv = Path.Combine(_dir, "pairs.csv");

        var pipeline = await DataPipeline.FromFileAsync(path);
        await pipeline.ToCsvAsync(csv);

        var lines = await File.ReadAllLinesAsync(csv);
        lines[0].Should().Be("Sentence1,Sentence2,Labels.Label,Labels.BinaryLabel,Tags");
        lines.Should().HaveCount(3);
    }

    [Fact]
    public async Task A_missing_file_is_reported_by_path()
    {
        var missing = Path.Combine(_dir, "none.parquet");

        var act = () => ParquetUtils.ReadParquetFileAsync(missing);

        (await act.Should().ThrowAsync<FileNotFoundException>()).WithMessage($"*{missing}*");
    }
}
