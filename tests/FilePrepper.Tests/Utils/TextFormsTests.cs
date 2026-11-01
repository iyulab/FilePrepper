using System.Text;
using FilePrepper.Pipeline;

namespace FilePrepper.Tests.Utils;

/// <summary>
/// The same text written whole (NFC) and as its letters (NFD) is one text: the readers of the library put a table in NFC where it is
/// read, so two files written in different forms concatenate, and a name written in one form finds the column written in the other.
/// </summary>
public class TextFormsTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;
    private static readonly string City = "서울";
    private static string Letters(string text) => text.Normalize(NormalizationForm.FormD);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private string Write(string name, string content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content, new UTF8Encoding(false));
        return path;
    }

    [Fact]
    public async Task A_data_frame_reads_letters_as_syllables()
    {
        var path = Write("frame.csv", $"{Letters("도시")},n\n{Letters(City)},1\n");

        var frame = await DataFrame.FromCsvAsync(path);

        Assert.Equal(["도시", "n"], frame.ColumnNames);
        Assert.Equal(City, frame.GetColumn("도시").Single());
    }

    [Fact]
    public async Task A_pipeline_reads_letters_as_syllables_and_concatenates_files_written_in_both_forms()
    {
        Write("a.csv", $"도시,n\n{City},1\n");
        Write("b.csv", $"{Letters("도시")},n\n{Letters(City)},2\n");

        var pipeline = await DataPipeline.ConcatCsvAsync("*.csv", _directory);

        Assert.Equal(["도시", "n"], pipeline.ColumnNames);
        Assert.Equal(2, pipeline.RowCount);
        Assert.All(pipeline.ToDataFrame().GetColumn("도시"), cell => Assert.Equal(City, cell));
    }

    [Fact]
    public async Task Two_columns_that_differ_only_in_form_are_one_name_and_are_refused()
    {
        var path = Write("twice.csv", $"도시,{Letters("도시")}\n1,2\n");

        await Assert.ThrowsAsync<InvalidDataException>(() => DataFrame.FromCsvAsync(path));
    }

    [Fact]
    public void The_two_forms_are_different_strings()
    {
        Assert.NotEqual(City, Letters(City));
    }
}
