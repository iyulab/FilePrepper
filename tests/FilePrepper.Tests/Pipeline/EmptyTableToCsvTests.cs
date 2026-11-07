using FilePrepper.Pipeline;

namespace FilePrepper.Tests.Pipeline;

/// <summary>
/// A table with no column has no record to write: <see cref="DataPipeline.ToCsvAsync"/> leaves an empty file rather than
/// asking the CSV writer for a record of no fields, which it refuses.
/// </summary>
public sealed class EmptyTableToCsvTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "fp-empty-" + Guid.NewGuid().ToString("N") + ".csv");

    public void Dispose()
    {
        try { File.Delete(_path); } catch { /* a locked temp file is not a test failure */ }
    }

    [Fact]
    public async Task ATableWithNoColumn_WritesAnEmptyFile()
    {
        await DataPipeline.FromData(Array.Empty<Dictionary<string, string>>()).ToCsvAsync(_path);

        Assert.True(File.Exists(_path));
        Assert.Equal(0, new FileInfo(_path).Length);
    }

    [Fact]
    public async Task ATableWithColumns_StillWritesItsHeaderAndRows()
    {
        await DataPipeline.FromData([new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" }]).ToCsvAsync(_path);

        Assert.Equal(["a,b", "1,2"], File.ReadAllLines(_path).Select(l => l.Trim()).ToArray());
    }
}
