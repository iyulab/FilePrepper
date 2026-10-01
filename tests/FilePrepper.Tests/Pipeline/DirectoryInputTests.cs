using FilePrepper.Pipeline;
using FilePrepper.Utils;
using FluentAssertions;

namespace FilePrepper.Tests.Pipeline;

/// <summary>
/// A folder of data files in one format is one table: the files' rows in file-name order, each file
/// read as it would be on its own.
/// </summary>
public class DirectoryInputTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"fp-dir-{Guid.NewGuid():N}");

    public DirectoryInputTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private void Write(string name, string content) => File.WriteAllText(Path.Combine(_dir, name), content);

    [Fact]
    public async Task One_record_per_file_reads_as_one_table_in_file_name_order()
    {
        // An export that writes one JSON object per measurement, named by its sequence number.
        Write("reading_00010.json", """{"t": 10, "temp": 21.5}""");
        Write("reading_00002.json", """{"t": 2, "temp": 20.0}""");
        Write("reading_00003.json", """{"temp": 20.4, "t": 3}""");
        Write(".done", "");

        var pipeline = await DataPipeline.FromDirectoryAsync(_dir);

        pipeline.ColumnNames.Should().Equal("t", "temp");
        pipeline.GetColumn("t").Should().Equal("2", "3", "10");
    }

    [Fact]
    public void Only_readable_files_count_and_they_are_ordered_by_name_ordinally()
    {
        Write("b.csv", "x\n1\n");
        Write("a-2.csv", "x\n2\n");
        Write("a_1.csv", "x\n3\n");
        Write("notes.done", "");
        Write("README.md", "# about");

        var files = DataFileFormats.FilesIn(_dir).Select(Path.GetFileName);

        // Ordinal: '-' (0x2D) sorts before '_' (0x5F); a culture-aware comparison may ignore the hyphen.
        files.Should().Equal("a-2.csv", "a_1.csv", "b.csv");
    }

    [Fact]
    public async Task Files_of_different_formats_are_refused_by_name()
    {
        Write("a.csv", "x\n1\n");
        Write("b.json", """[{"x": 2}]""");

        var act = () => DataPipeline.FromDirectoryAsync(_dir);

        (await act.Should().ThrowAsync<InvalidDataException>())
            .Which.Message.Should().Contain("a.csv").And.Contain("b.json");
    }

    [Fact]
    public async Task A_file_with_other_columns_is_refused_with_the_columns_that_differ()
    {
        Write("a.csv", "x,y\n1,2\n");
        Write("b.csv", "x,z\n3,4\n");

        var act = () => DataPipeline.FromDirectoryAsync(_dir);

        (await act.Should().ThrowAsync<InvalidDataException>())
            .Which.Message.Should().Contain("b.csv").And.Contain("y").And.Contain("z");
    }

    [Fact]
    public async Task A_folder_with_no_data_file_is_refused()
    {
        Write("README.md", "# about");

        var act = () => DataPipeline.FromDirectoryAsync(_dir);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task A_record_path_applies_to_every_file()
    {
        Write("a.json", """{"data": [{"q": "one"}]}""");
        Write("b.json", """{"data": [{"q": "two"}, {"q": "three"}]}""");

        var pipeline = await DataPipeline.FromDirectoryAsync(_dir, jsonRecordPath: "data");

        pipeline.GetColumn("q").Should().Equal("one", "two", "three");
    }
}
