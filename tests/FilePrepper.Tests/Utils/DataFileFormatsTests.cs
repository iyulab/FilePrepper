using FilePrepper.Utils;
using OfficeOpenXml;

namespace FilePrepper.Tests.Utils;

/// <summary>
/// A file whose extension names no format is read as delimited text — and an Office Open XML workbook saved as a macro-enabled
/// workbook (<c>.xlsm</c>) or a template (<c>.xltx</c>, <c>.xltm</c>) is a zip, so read as text it came back as tens of thousands of
/// "rows" of archive bytes under a header such as <c>PK\u0003\u0004…</c>, with no error. These are the same zip of XML parts a
/// plain workbook is; and a zip that is no workbook is refused, as XML is, rather than read as nonsense.
/// </summary>
public class DataFileFormatsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"fp-formats-{Guid.NewGuid():N}");

    public DataFileFormatsTests()
    {
        Directory.CreateDirectory(_dir);
        ExcelPackage.License.SetNonCommercialPersonal("FilePrepper.Tests");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private string Workbook(string name)
    {
        var path = Path.Combine(_dir, name);
        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add("data");
        sheet.Cells[1, 1].Value = "a";
        sheet.Cells[1, 2].Value = "b";
        sheet.Cells[2, 1].Value = 1;
        sheet.Cells[2, 2].Value = "x";
        sheet.Cells[3, 1].Value = 2;
        sheet.Cells[3, 2].Value = "y";
        var source = Path.Combine(_dir, "source-workbook.xlsx");
        package.SaveAs(new FileInfo(source));
        File.Copy(source, path, overwrite: true);
        return path;
    }

    [Theory]
    [InlineData("book.xlsm")]
    [InlineData("book.xltx")]
    [InlineData("book.xltm")]
    [InlineData("book.XLSX")]
    public void AWorkbookVariant_IsExcel(string name)
        => Assert.Equal(DataFileFormat.Excel, DataFileFormats.FromPath(Workbook(name)));

    [Theory]
    [InlineData("book.xlsm")]
    [InlineData("book.xltx")]
    public async Task AWorkbookVariant_IsReadAsATable(string name)
    {
        var (records, headers) = await ExcelUtils.ReadExcelFileAsync(Workbook(name));

        Assert.Equal(["a", "b"], headers);
        Assert.Equal(2, records.Count);
        Assert.True(ExcelUtils.IsExcelFile(name));
    }

    [Fact]
    public void AWorkbookWithAnUnknownExtension_IsExcelByItsContent()
        => Assert.Equal(DataFileFormat.Excel, DataFileFormats.FromPath(Workbook("export.dat")));

    [Fact]
    public void AZipThatIsNoWorkbook_IsRefusedNotReadAsText()
    {
        var path = Path.Combine(_dir, "images.dat");
        using (var zip = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Create))
            zip.CreateEntry("a.txt");

        var error = Assert.Throws<NotSupportedException>(() => DataFileFormats.FromPath(path));

        Assert.Contains("zip", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("images.dat", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DelimitedTextWithAnUnknownExtension_IsStillCsv()
    {
        var path = Path.Combine(_dir, "table.dat");
        File.WriteAllText(path, "a,b\n1,2\n");

        Assert.Equal(DataFileFormat.Csv, DataFileFormats.FromPath(path));
    }

    [Fact]
    public void ANameThatDoesNotExistYet_IsStillClassifiedByItsExtension()
        => Assert.Equal(DataFileFormat.Csv, DataFileFormats.FromPath(Path.Combine(_dir, "missing.out")));
}
