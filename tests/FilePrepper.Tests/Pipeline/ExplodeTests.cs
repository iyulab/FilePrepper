using FilePrepper.Pipeline;
using FilePrepper.Tasks;
using FilePrepper.Tasks.Explode;
using FilePrepper.Utils;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Parquet.Serialization;

namespace FilePrepper.Tests.Pipeline;

/// <summary>
/// One row per element of list columns: a group row with parallel lists (a query and its
/// documents' grades and features) becomes one row per document.
/// </summary>
public class ExplodeTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"fp-explode-{Guid.NewGuid():N}");

    public ExplodeTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    public sealed class Query
    {
        public long Id { get; set; }
        public List<double> Labels { get; set; } = [];
        public List<string> Docs { get; set; } = [];
        public List<List<double>> Features { get; set; } = [];
    }

    private async Task<string> WriteAsync(params Query[] rows)
    {
        var path = Path.Combine(_dir, "queries.parquet");
        await ParquetSerializer.SerializeAsync(rows, path);
        return path;
    }

    private static Query Seven() => new()
    {
        Id = 7,
        Labels = [2, 0, 1],
        Docs = ["a", "b", "c"],
        Features = [[0.5, 1], [0.25, 0], [1.5, 2]],
    };

    private async Task<(bool Success, List<string[]> Rows)> ExplodeFileAsync(string input, params string[] columns)
    {
        var output = Path.Combine(_dir, "out.csv");
        var task = new ExplodeTask(NullLogger<ExplodeTask>.Instance);
        var success = await task.ExecuteAsync(new TaskContext(new ExplodeOption
        {
            InputPath = input,
            OutputPath = output,
            Columns = [.. columns],
        }));
        var rows = success
            ? (await File.ReadAllLinesAsync(output)).Select(l => l.Split(',', 2)).ToList()
            : [];
        return (success, rows);
    }

    [Fact]
    public async Task Parallel_lists_read_from_parquet_become_one_row_per_element()
    {
        var input = await WriteAsync(Seven());
        var output = Path.Combine(_dir, "out.csv");

        var success = await new ExplodeTask(NullLogger<ExplodeTask>.Instance).ExecuteAsync(new TaskContext(new ExplodeOption
        {
            InputPath = input,
            OutputPath = output,
            Columns = ["Labels", "Docs", "Features"],
        }));

        success.Should().BeTrue();
        var pipeline = await DataPipeline.FromCsvAsync(output);
        pipeline.ColumnNames.Should().Equal("Id", "Labels", "Docs", "Features");
        var rows = pipeline.ToDataFrame().Rows;
        rows.Select(r => r["Id"]).Should().Equal("7", "7", "7");
        rows.Select(r => r["Labels"]).Should().Equal("2", "0", "1");
        rows.Select(r => r["Docs"]).Should().Equal("a", "b", "c");
        // An element that is itself a list stays JSON text.
        rows.Select(r => r["Features"]).Should().Equal("[0.5,1]", "[0.25,0]", "[1.5,2]");
    }

    [Fact]
    public void Lists_of_different_length_in_one_row_are_refused_with_the_lengths()
    {
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["q"] = "1", ["a"] = "[1,2]", ["b"] = "[\"x\"]" },
        };

        var act = () => ListColumns.Explode(rows, ["q", "a", "b"], ["a", "b"]);

        act.Should().Throw<ArgumentException>().WithMessage("Row 1: *same length*a=2, b=1*");
    }

    [Fact]
    public void An_empty_list_or_empty_cell_keeps_the_row_with_the_lists_empty()
    {
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["q"] = "1", ["a"] = "[]" },
            new() { ["q"] = "2", ["a"] = "" },
            new() { ["q"] = "3", ["a"] = "[null,\"\",true]" },
        };

        var exploded = ListColumns.Explode(rows, ["q", "a"], ["a"]);

        exploded.Select(r => (r["q"], r["a"])).Should().Equal(
            ("1", ""), ("2", ""), ("3", ""), ("3", ""), ("3", "true"));
    }

    [Fact]
    public void A_cell_that_is_not_a_list_is_refused()
    {
        var rows = new List<Dictionary<string, string>> { new() { ["a"] = "{\"k\":1}" } };

        var act = () => ListColumns.Explode(rows, ["a"], ["a"]);

        act.Should().Throw<ArgumentException>().WithMessage("Row 1, column 'a'*not a JSON list*");
    }

    [Fact]
    public void A_column_not_in_the_data_is_refused()
    {
        var act = () => DataPipeline.FromData([new() { ["a"] = "[1]" }]).Explode("b");

        act.Should().Throw<ArgumentException>().WithMessage("Column 'b' not found*");
    }

    [Fact]
    public void The_pipeline_keeps_the_column_order()
    {
        var pipeline = DataPipeline.FromData([new() { ["a"] = "[1,2]", ["q"] = "9", ["b"] = "[3,4]" }])
            .Explode("a", "b");

        pipeline.ColumnNames.Should().Equal("a", "q", "b");
        pipeline.ToDataFrame().Rows.Select(r => (r["a"], r["q"], r["b"])).Should().Equal(("1", "9", "3"), ("2", "9", "4"));
    }

    [Fact]
    public async Task The_task_fails_on_a_length_mismatch_rather_than_writing_a_partial_file()
    {
        var input = Path.Combine(_dir, "in.csv");
        await File.WriteAllTextAsync(input, "q,a,b\n1,\"[1,2]\",\"[3]\"\n");

        var act = () => ExplodeFileAsync(input, "a", "b");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("Row 1: *");
        File.Exists(Path.Combine(_dir, "out.csv")).Should().BeFalse();
    }
}
