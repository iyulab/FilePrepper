using System.Text.Json;
using FilePrepper.Pipeline;
using FilePrepper.Utils;

namespace FilePrepper.Tests.Utils;

/// <summary>
/// JSON Lines — one JSON object per line — is how logs and dataset exports are written. It read as delimited text
/// (a single column headed by its first record) when named <c>.jsonl</c>, and as an unreadable document under <c>.json</c>
/// ("'{' is invalid after a single JSON value"): both now read as the table the lines are.
/// </summary>
public class JsonLinesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"fp-jsonl-{Guid.NewGuid():N}");

    public JsonLinesTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private string Write(string name, string text)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, text);
        return path;
    }

    private const string Lines = "{\"a\": 1, \"kind\": \"red\"}\n{\"a\": 2.5, \"kind\": \"blue\"}\r\n\n{\"a\": 3, \"kind\": null}\n";

    [Theory]
    [InlineData("rows.jsonl")]
    [InlineData("rows.ndjson")]
    [InlineData("ROWS.JSONL")]
    [InlineData("rows.json")]
    public async Task One_object_per_line_is_a_table_whatever_it_is_named(string name)
    {
        var (records, headers) = await JsonUtils.ReadJsonFileAsync(Write(name, Lines));

        Assert.Equal(["a", "kind"], headers);
        Assert.Equal(3, records.Count);
        Assert.Equal("2.5", records[1]["a"]);
        Assert.Equal("", records[2]["kind"]);   // null is an empty cell, as in a JSON array
    }

    [Theory]
    [InlineData("rows.jsonl", DataFileFormat.Json)]
    [InlineData("rows.ndjson", DataFileFormat.Json)]
    [InlineData("rows.ndjson.csv", DataFileFormat.Csv)]
    public void The_extensions_name_the_JSON_reader(string name, DataFileFormat expected)
        => Assert.Equal(expected, DataFileFormats.FromPath(Path.Combine(_dir, name)));

    [Fact]
    public async Task A_pipeline_opens_a_jsonl_file_as_a_table()
    {
        var pipeline = await DataPipeline.FromFileAsync(Write("rows.jsonl", Lines));

        Assert.Equal(3, pipeline.RowCount);
    }

    [Fact]
    public async Task A_line_that_is_no_json_is_named_by_its_number_in_a_jsonl_file()
    {
        var error = await Assert.ThrowsAsync<JsonException>(() =>
            JsonUtils.ReadJsonFileAsync(Write("bad.jsonl", "{\"a\": 1}\n{\"a\": \n{\"a\": 3}\n")));

        Assert.Contains("line 2", error.Message, StringComparison.Ordinal);
        Assert.Contains("JSON Lines", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_json_file_that_is_neither_one_document_nor_json_lines_keeps_an_error_that_says_what_is_read()
    {
        var error = await Assert.ThrowsAsync<JsonException>(() =>
            JsonUtils.ReadJsonFileAsync(Write("bad.json", "{\"a\": 1}\nnot json at all\n")));

        Assert.Contains("array of objects", error.Message, StringComparison.Ordinal);
        Assert.Contains(".jsonl", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_json_array_and_a_single_object_are_read_as_before()
    {
        var (arrayRows, _) = await JsonUtils.ReadJsonFileAsync(Write("a.json", "[{\"x\": 1}, {\"x\": 2}]"));
        var (objectRows, _) = await JsonUtils.ReadJsonFileAsync(Write("o.json", "{\"x\": 1}"));

        Assert.Equal(2, arrayRows.Count);
        Assert.Single(objectRows);
    }

    [Fact]
    public async Task A_record_path_walks_every_line_as_a_document()
    {
        var path = Write("nested.jsonl", "{\"id\": 1, \"items\": [{\"v\": 1}, {\"v\": 2}]}\n{\"id\": 2, \"items\": [{\"v\": 3}]}\n");

        var (records, headers) = await JsonUtils.ReadJsonFileAsync(path, "items");

        Assert.Equal(3, records.Count);
        Assert.Contains("v", headers);
    }
}
