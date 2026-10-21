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

    [Fact]
    public async Task A_query_per_row_file_becomes_a_document_per_row_with_feature_columns()
    {
        // The whole path: group rows with parallel lists → one row per document → fixed-width
        // feature lists as columns.
        var exploded = Path.Combine(_dir, "exploded.csv");
        var output = Path.Combine(_dir, "docs.csv");
        await new ExplodeTask(NullLogger<ExplodeTask>.Instance).ExecuteAsync(new TaskContext(new ExplodeOption
        {
            InputPath = await WriteAsync(Seven()),
            OutputPath = exploded,
            Columns = ["Labels", "Docs", "Features"],
        }));

        var success = await new FilePrepper.Tasks.SplitList.SplitListTask(
            NullLogger<FilePrepper.Tasks.SplitList.SplitListTask>.Instance).ExecuteAsync(new TaskContext(
            new FilePrepper.Tasks.SplitList.SplitListOption { InputPath = exploded, OutputPath = output, Columns = ["Features"] }));

        success.Should().BeTrue();
        (await File.ReadAllLinesAsync(output)).Select(l => l.TrimStart('\uFEFF')).Should().Equal(
            "Id,Labels,Docs,Features.0,Features.1",
            "7,2,a,0.5,1",
            "7,0,b,0.25,0",
            "7,1,c,1.5,2");
    }

    [Fact]
    public void Split_lists_of_another_length_are_refused_with_the_row()
    {
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["f"] = "[1,2]" },
            new() { ["f"] = "" },
            new() { ["f"] = "[1,2,3]" },
        };

        var act = () => ListColumns.SplitToColumns(rows, ["f"], ["f"]);

        act.Should().Throw<ArgumentException>().WithMessage("Row 3, column 'f': a list of 3 where earlier rows have 2*");
    }

    [Fact]
    public void Split_keeps_the_column_position_and_leaves_an_empty_cell_empty()
    {
        var pipeline = DataPipeline.FromData(
        [
            new() { ["q"] = "1", ["f"] = "[[1],\"x\"]", ["y"] = "a" },
            new() { ["q"] = "2", ["f"] = "", ["y"] = "b" },
        ]).SplitList("f");

        pipeline.ColumnNames.Should().Equal("q", "f.0", "f.1", "y");
        var rows = pipeline.ToDataFrame().Rows;
        rows[0]["f.0"].Should().Be("[1]");
        rows[0]["f.1"].Should().Be("x");
        rows[1]["f.0"].Should().BeEmpty();
    }

    [Fact]
    public void Split_refuses_a_name_that_is_already_a_column()
    {
        var act = () => DataPipeline.FromData([new() { ["f"] = "[1]", ["f.0"] = "z" }]).SplitList("f");

        act.Should().Throw<ArgumentException>().WithMessage("*second column named 'f.0'*");
    }
}
