using System.Text;
using FilePrepper.Pipeline;
using FluentAssertions;
using Xunit;

namespace FilePrepper.Tests.Pipeline;

/// <summary>
/// DataPipeline 의 CSV 진입점이 비-UTF8 인코딩(CP949 등)을 mojibake 없이 읽는지 검증.
/// 라이브러리의 다른 진입점(BaseTask, MergeOption, CLI)이 이미 인코딩을 처리하는 것과
/// 일관성을 보장한다.
/// </summary>
public class DataPipelineEncodingTests
{
    public DataPipelineEncodingTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static Encoding Cp949 => Encoding.GetEncoding(949);

    private static async Task<string> WriteCp949CsvAsync(string dir, string fileName, string content)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        await File.WriteAllBytesAsync(path, Cp949.GetBytes(content));
        return path;
    }

    [Fact]
    public async Task FromCsvAsync_AutoDetectsCp949_NoMojibake()
    {
        var testDir = Path.Combine("TestData", $"Cp949_From_{Guid.NewGuid():N}");
        var path = await WriteCp949CsvAsync(testDir, "korean.csv",
            "이름,점수\n홍길동,85\n김철수,90");

        var pipeline = await DataPipeline.FromCsvAsync(path);

        pipeline.ColumnNames.Should().BeEquivalentTo(new[] { "이름", "점수" });
        pipeline.RowCount.Should().Be(2);
        var df = pipeline.ToDataFrame();
        df.Rows[0]["이름"].Should().Be("홍길동");
        df.Rows[1]["이름"].Should().Be("김철수");
    }

    [Fact]
    public async Task FromCsvAsync_ExplicitCp949Encoding_NoMojibake()
    {
        var testDir = Path.Combine("TestData", $"Cp949_FromExplicit_{Guid.NewGuid():N}");
        var path = await WriteCp949CsvAsync(testDir, "korean.csv",
            "도시,인구\n서울,9700000\n부산,3400000");

        var pipeline = await DataPipeline.FromCsvAsync(path, hasHeader: true, encoding: "cp949");

        pipeline.ColumnNames.Should().BeEquivalentTo(new[] { "도시", "인구" });
        var df = pipeline.ToDataFrame();
        df.Rows[0]["도시"].Should().Be("서울");
    }

    [Fact]
    public async Task FromCsvAsync_DefaultAuto_PreservesUtf8Behavior()
    {
        var testDir = Path.Combine("TestData", $"Utf8_From_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);
        var path = Path.Combine(testDir, "ascii.csv");
        await File.WriteAllTextAsync(path, "Name,Age\nAlice,25\nBob,30", Encoding.UTF8);

        var pipeline = await DataPipeline.FromCsvAsync(path);

        pipeline.ColumnNames.Should().BeEquivalentTo(new[] { "Name", "Age" });
        pipeline.RowCount.Should().Be(2);
    }

    [Fact]
    public async Task ConcatCsvAsync_AutoDetectsCp949AcrossFiles_NoMojibake()
    {
        var testDir = Path.Combine("TestData", $"Cp949_Concat_{Guid.NewGuid():N}");
        await WriteCp949CsvAsync(testDir, "data-1.csv", "이름,점수\n홍길동,85");
        await WriteCp949CsvAsync(testDir, "data-2.csv", "이름,점수\n김철수,90");

        var pipeline = await DataPipeline.ConcatCsvAsync("data-*.csv", testDir);

        pipeline.RowCount.Should().Be(2);
        var df = pipeline.ToDataFrame();
        df.Rows.Select(r => r["이름"]).Should().BeEquivalentTo(new[] { "홍길동", "김철수" });
    }

    [Fact]
    public async Task ConcatCsvAsync_ExplicitCp949Encoding_NoMojibake()
    {
        var testDir = Path.Combine("TestData", $"Cp949_ConcatExplicit_{Guid.NewGuid():N}");
        await WriteCp949CsvAsync(testDir, "data-1.csv", "이름,점수\n홍길동,85");
        await WriteCp949CsvAsync(testDir, "data-2.csv", "이름,점수\n김철수,90");

        var pipeline = await DataPipeline.ConcatCsvAsync(
            "data-*.csv",
            testDir,
            hasHeader: true,
            addSourceColumn: false,
            sourceColumnName: "SourceFile",
            encoding: "cp949");

        pipeline.RowCount.Should().Be(2);
        var df = pipeline.ToDataFrame();
        df.Rows[0]["이름"].Should().Be("홍길동");
    }
}
