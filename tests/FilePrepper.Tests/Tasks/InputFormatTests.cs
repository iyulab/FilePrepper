using FilePrepper.Tasks;
using FilePrepper.Tasks.FileFormatConvert;
using FilePrepper.Utils;
using Microsoft.Extensions.Logging;
using Moq;

namespace FilePrepper.Tests.Tasks;

/// <summary>
/// A task reads its input by the file's format, the same way the pipeline does — not every
/// non-Excel file as comma-separated text.
/// </summary>
public class InputFormatTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"fp-input-{Guid.NewGuid():N}");

    public InputFormatTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private string[] ConvertToCsv(string inputName, string content)
    {
        var input = Path.Combine(_dir, inputName);
        var output = Path.Combine(_dir, "out.csv");
        File.WriteAllText(input, content);

        var task = new FileFormatConvertTask(new Mock<ILogger<FileFormatConvertTask>>().Object);
        var ok = task.Execute(new TaskContext(new FileFormatConvertOption
        {
            InputPath = input,
            OutputPath = output,
            TargetFormat = FileFormat.CSV,
            HasHeader = true
        }));

        Assert.True(ok);
        return File.ReadAllLines(output);
    }

    [Fact]
    public void A_json_array_is_read_as_json()
    {
        var lines = ConvertToCsv("in.json", """[{"a":1,"b":"x"},{"a":2,"b":null}]""");

        Assert.Equal(["a,b", "1,x", "2,"], lines.Select(l => l.TrimStart('﻿')));
    }

    [Fact]
    public void A_tsv_file_is_split_on_tabs()
    {
        var lines = ConvertToCsv("in.tsv", "name\tcity\nKim, J\tSeoul\n");

        Assert.Equal(["name,city", "\"Kim, J\",Seoul"], lines.Select(l => l.TrimStart('﻿')));
    }

    [Theory]
    [InlineData("data.csv", DataFileFormat.Csv)]
    [InlineData("data.TSV", DataFileFormat.Tsv)]
    [InlineData("data.json", DataFileFormat.Json)]
    [InlineData("data.xlsx", DataFileFormat.Excel)]
    [InlineData("data.xls", DataFileFormat.Excel)]
    [InlineData("data.parquet", DataFileFormat.Parquet)]
    [InlineData("data.txt", DataFileFormat.Csv)]
    public void The_format_follows_the_extension(string path, DataFileFormat expected) =>
        Assert.Equal(expected, DataFileFormats.FromPath(path));

    [Fact]
    public void Xml_is_refused_as_input_rather_than_read_as_text()
    {
        Assert.False(DataFileFormats.IsReadable("data.xml"));
        var ex = Assert.Throws<NotSupportedException>(() => DataFileFormats.FromPath("data.xml"));
        Assert.Contains("output format only", ex.Message);
    }

    [Fact]
    public async Task A_json_document_with_its_rows_under_a_property_is_described_not_dumped()
    {
        var path = Path.Combine(_dir, "doc.json");
        File.WriteAllText(path, """{"meta": 1, "annotation": [{"text": "a", "label": "x"}]}""");

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => JsonUtils.ReadJsonFileAsync(path));

        Assert.Contains("array of objects", ex.Message);
        Assert.Contains("'annotation' (1 items)", ex.Message);
    }
}
