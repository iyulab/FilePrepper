using FilePrepper.Utils;

namespace FilePrepper.Tests.Utils;

/// <summary>
/// A JSON document that is one object is one row — the shape of exports that write a file per record
/// (a sensor reading per timestamp) — unless it keeps rows of its own in an array of objects.
/// </summary>
public class JsonObjectRowTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"fp-jsonrow-{Guid.NewGuid():N}");

    public JsonObjectRowTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private string Write(string json)
    {
        var path = Path.Combine(_dir, "record.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public async Task An_object_of_values_is_one_row()
    {
        var (records, headers) = await JsonUtils.ReadJsonFileAsync(Write("""{"id": "00002", "volts": 3.295, "flag": "N", "gone": null}"""));

        Assert.Equal(["id", "volts", "flag", "gone"], headers);
        var row = Assert.Single(records);
        Assert.Equal("00002", row["id"]);
        Assert.Equal("3.295", row["volts"]);
        Assert.Equal("", row["gone"]);
    }

    [Fact]
    public async Task A_dotted_field_name_is_kept_as_written()
    {
        var (records, headers) = await JsonUtils.ReadJsonFileAsync(Write("""{"indu_t": 26.9, "indu_t.1": 27.0}"""));

        Assert.Equal(["indu_t", "indu_t.1"], headers);
        Assert.Equal("27.0", Assert.Single(records)["indu_t.1"]);
    }

    [Fact]
    public async Task Arrays_of_values_and_nested_objects_are_fields_of_the_row()
    {
        var (records, _) = await JsonUtils.ReadJsonFileAsync(Write("""{"id": 1, "tags": [1, 2], "where": {"x": 1}, "items": []}"""));

        var row = Assert.Single(records);
        Assert.Equal("[1, 2]", row["tags"]);
        Assert.Equal("""{"x": 1}""", row["where"]);
        Assert.Equal("[]", row["items"]);
    }

    [Fact]
    public async Task An_empty_object_is_an_empty_table()
    {
        var (records, headers) = await JsonUtils.ReadJsonFileAsync(Write("{}"));

        Assert.Empty(records);
        Assert.Empty(headers);
    }

    [Fact]
    public async Task An_object_holding_rows_is_refused_with_the_paths_that_read_them()
    {
        var ex = await Assert.ThrowsAsync<JsonShapeException>(() =>
            JsonUtils.ReadJsonFileAsync(Write("""{"meta": 1, "data": [{"rows": [{"x": 1}]}]}""")));

        Assert.Equal(["data", "data.rows"], ex.RecordPaths);
    }

    [Fact]
    public async Task A_document_with_no_rows_to_point_at_names_no_record_path()
    {
        var ex = await Assert.ThrowsAsync<JsonShapeException>(() => JsonUtils.ReadJsonFileAsync(Write("[1, 2, 3]")));

        Assert.Empty(ex.RecordPaths);
    }
}
