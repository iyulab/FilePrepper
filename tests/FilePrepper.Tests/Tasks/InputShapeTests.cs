using FilePrepper.Pipeline;
using FilePrepper.Tasks;
using FilePrepper.Tasks.RemoveColumns;
using FilePrepper.Tasks.ReorderColumns;

namespace FilePrepper.Tests.Tasks;

/// <summary>
/// Two inputs a task could not make a table of used to be read as if they were one. A file whose header names a column twice kept
/// only the last of the two (the row held by name was overwritten, the first column left the output) and an empty file became a
/// one-line output headed with the word <c>NoData</c>; both reported success. Both are refused where the input is read.
/// </summary>
public class InputShapeTests : TaskBaseTest<RemoveColumnsTask>
{
    public InputShapeTests(ITestOutputHelper output) : base(output)
    {
    }

    private RemoveColumnsOption Option() => new() { InputPath = _testInputPath, OutputPath = _testOutputPath, RemoveColumns = ["c"] };

    [Fact]
    public void A_header_that_names_a_column_twice_is_refused_with_the_name_and_the_positions()
    {
        WriteTestFileLines("a,b,b,c", "1,2,3,4");

        var error = Assert.Throws<InvalidDataException>(() => new RemoveColumnsTask(_mockLogger.Object).Execute(new TaskContext(Option())));

        Assert.Contains("'b' (columns 2, 3)", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, new FileInfo(_testOutputPath).Length);
    }

    [Fact]
    public void A_header_whose_names_differ_only_by_case_is_two_columns()
    {
        WriteTestFileLines("a,A,c", "1,2,3");

        Assert.True(new RemoveColumnsTask(_mockLogger.Object).Execute(new TaskContext(Option())));

        Assert.Equal(["a,A", "1,2"], ReadOutputFileLines());
    }

    [Fact]
    public void An_empty_file_is_refused_instead_of_written_as_a_placeholder_table()
    {
        WriteTestFile(string.Empty);

        var error = Assert.Throws<InvalidDataException>(() => new RemoveColumnsTask(_mockLogger.Object).Execute(new TaskContext(Option())));

        Assert.Contains("has no columns", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, new FileInfo(_testOutputPath).Length);
    }

    [Fact]
    public void An_empty_file_is_refused_by_reorder_columns_too()
    {
        WriteTestFile(string.Empty);
        var option = new ReorderColumnsOption { InputPath = _testInputPath, OutputPath = _testOutputPath, Order = ["a"] };

        Assert.Throws<InvalidDataException>(() => new ReorderColumnsTask(new Moq.Mock<Microsoft.Extensions.Logging.ILogger<ReorderColumnsTask>>().Object).Execute(new TaskContext(option)));

        Assert.Equal(0, new FileInfo(_testOutputPath).Length);
    }

    [Fact]
    public async Task A_data_frame_and_a_pipeline_refuse_a_header_that_names_a_column_twice()
    {
        WriteTestFileLines("a,b,b", "1,2,3");

        await Assert.ThrowsAsync<InvalidDataException>(() => DataFrame.FromCsvAsync(_testInputPath));
        await Assert.ThrowsAsync<InvalidDataException>(() => DataPipeline.FromCsvAsync(_testInputPath));
    }
}
