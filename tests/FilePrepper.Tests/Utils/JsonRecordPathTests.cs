using FilePrepper.Pipeline;
using FilePrepper.Utils;

namespace FilePrepper.Tests.Utils;

/// <summary>
/// A record path reads rows that sit in a nested array, each carrying the fields of the items it was
/// reached through — the shape of reading-comprehension data, where a question and its passage are stored
/// at different levels.
/// </summary>
public class JsonRecordPathTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"fp-jsonpath-{Guid.NewGuid():N}");

    public JsonRecordPathTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private string Write(string json)
    {
        var path = Path.Combine(_dir, "doc.json");
        File.WriteAllText(path, json);
        return path;
    }

    private const string Squad = """
        {
          "version": "1.0",
          "data": [
            { "title": "t1", "paragraphs": [
              { "context": "Seoul is the capital.", "qas": [
                { "id": "q1", "question": "What is the capital?", "answers": [ { "text": "Seoul", "answer_start": 0 } ] },
                { "id": "q2", "question": "Which city?", "answers": [ { "text": "Seoul", "answer_start": 0 } ], "clue": [ { "clue_text": "capital" } ] }
              ] }
            ] }
          ]
        }
        """;

    [Fact]
    public async Task Each_answer_is_a_row_that_keeps_its_question_and_passage()
    {
        var (records, headers) = await JsonUtils.ReadJsonFileAsync(Write(Squad), "data.paragraphs.qas.answers");

        Assert.Equal(2, records.Count);
        Assert.Equal("Seoul is the capital.", records[0]["paragraphs.context"]);
        Assert.Equal("What is the capital?", records[0]["qas.question"]);
        Assert.Equal("Seoul", records[0]["text"]);
        Assert.Equal("0", records[0]["answer_start"]);
        Assert.Equal("t1", records[0]["data.title"]);
        // The file's own fields describe no row.
        Assert.DoesNotContain("version", headers);
        // A field only some rows have is an empty cell in the others, as in CSV; an array becomes JSON text.
        Assert.Equal(string.Empty, records[0]["qas.clue"]);
        Assert.Contains("capital", records[1]["qas.clue"]);
    }

    [Fact]
    public async Task The_pipeline_reads_the_same_rows()
    {
        var pipeline = await DataPipeline.FromFileAsync(Write(Squad), jsonRecordPath: "data.paragraphs.qas");

        Assert.Equal(2, pipeline.RowCount);
        Assert.Contains("paragraphs.context", pipeline.ColumnNames);
        Assert.Contains("question", pipeline.ColumnNames);
    }

    [Fact]
    public async Task A_path_that_does_not_lead_to_an_array_names_what_is_there()
    {
        var ex = await Assert.ThrowsAsync<InvalidDataException>(() =>
            JsonUtils.ReadJsonFileAsync(Write(Squad), "data.passages"));

        Assert.Contains("'data.passages'", ex.Message);
        Assert.Contains("'paragraphs'", ex.Message);
    }

    [Fact]
    public async Task A_top_level_array_is_walked_document_by_document()
    {
        var path = Write("""[ { "doc": "a", "rows": [ { "x": 1 }, { "x": 2 } ] }, { "doc": "b", "rows": [ { "x": 3 } ] } ]""");

        var (records, _) = await JsonUtils.ReadJsonFileAsync(path, "rows");

        Assert.Equal(["1", "2", "3"], records.Select(r => r["x"]));
    }

    [Fact]
    public async Task A_document_that_is_not_a_table_names_the_record_paths_that_would_read_it()
    {
        var ex = await Assert.ThrowsAsync<JsonShapeException>(() => JsonUtils.ReadJsonFileAsync(Write(Squad)));

        Assert.Contains("'data.paragraphs.qas.answers'", ex.Message);
    }
}
