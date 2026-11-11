namespace FilePrepper.Utils;

/// <summary>The formats FilePrepper reads data from.</summary>
public enum DataFileFormat
{
    Csv,
    Tsv,
    Json,
    Excel,
    Parquet,
    SvmLight
}

/// <summary>
/// Which reader a data file gets — decided here once, so a command, a task and the pipeline
/// cannot disagree about what a file is.
/// </summary>
public static class DataFileFormats
{
    /// <summary>
    /// The workbook extensions, lower-case with the dot. <c>.xls</c> is the binary format; the others are Office Open XML — a
    /// macro-enabled workbook (<c>.xlsm</c>) and a template (<c>.xltx</c>, <c>.xltm</c>) are the same zip of XML parts a plain
    /// workbook is, and one reader (ExcelDataReader's) opens them all.
    /// </summary>
    public static readonly IReadOnlyList<string> ExcelExtensions = [".xlsx", ".xlsm", ".xltx", ".xltm", ".xls"];

    /// <summary>JSON Lines: one JSON object per line. Read by the JSON reader (<see cref="DataFileFormat.Json"/>).</summary>
    public static readonly IReadOnlyList<string> JsonLinesExtensions = [".jsonl", ".ndjson"];

    /// <summary>Extensions FilePrepper can read, lower-case with the dot.</summary>
    public static readonly IReadOnlyList<string> ReadableExtensions =
        [".csv", ".tsv", ".json", .. JsonLinesExtensions, .. ExcelExtensions, ".parquet", ".svm", ".svmlight", ".libsvm"];

    /// <summary>
    /// The format of <paramref name="path"/> by its extension. An extension that names no format is
    /// read as CSV, as delimited text usually is whatever it is called — unless the file's lines are
    /// SVMlight (<c>label index:value …</c>), which learning-to-rank sets publish under names such as
    /// <c>rank.train</c> and which read as CSV would be one column headed by its first row.
    /// </summary>
    /// <exception cref="NotSupportedException">The file is XML, which FilePrepper writes but does
    /// not read, or a zip that is no workbook (an archive, or another kind of Office document) — read as delimited text either
    /// would come back as nonsense rather than an error. A workbook under an extension that names none is Excel by its content.</exception>
    public static DataFileFormat FromPath(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (ExcelExtensions.Contains(extension))
            return DataFileFormat.Excel;
        if (JsonLinesExtensions.Contains(extension))
            return DataFileFormat.Json;
        return extension switch
        {
            ".xml" => throw new NotSupportedException(
                $"XML is an output format only; FilePrepper reads {string.Join(", ", ReadableExtensions)}. ({path})"),
            ".tsv" => DataFileFormat.Tsv,
            ".json" => DataFileFormat.Json,
            ".parquet" => DataFileFormat.Parquet,
            ".svm" or ".svmlight" or ".libsvm" => DataFileFormat.SvmLight,
            _ => ByContent(path),
        };
    }

    // An extension that names no format: a zip is a workbook or something FilePrepper does not read; svmlight is told by its
    // lines; anything else is delimited text.
    /// <summary>Whether <paramref name="path"/> is named as a JSON Lines file.</summary>
    public static bool IsJsonLines(string path) => JsonLinesExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    private static DataFileFormat ByContent(string path)
    {
        if (IsZip(path))
            return IsWorkbook(path)
                ? DataFileFormat.Excel
                : throw new NotSupportedException(
                    $"This is a zip container that holds no workbook (an archive, or another kind of Office document), not a table; FilePrepper reads {string.Join(", ", ReadableExtensions)}. ({path})");
        return SvmLightUtils.LooksLikeSvmLight(path) ? DataFileFormat.SvmLight : DataFileFormat.Csv;
    }

    private static bool IsZip(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> magic = stackalloc byte[4];
            return stream.Read(magic) == 4 && magic[0] == 0x50 && magic[1] == 0x4B && magic[2] == 0x03 && magic[3] == 0x04;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;   // a name that does not exist yet, or cannot be opened: the extension is all there is to go by
        }
    }

    private static bool IsWorkbook(string path)
    {
        try
        {
            using var zip = System.IO.Compression.ZipFile.OpenRead(path);
            return zip.GetEntry("xl/workbook.xml") is not null || zip.GetEntry("xl/workbook.bin") is not null;
        }
        catch (System.IO.InvalidDataException)
        {
            return false;
        }
    }

    /// <summary>Whether <paramref name="path"/> has an extension FilePrepper reads, or is SVMlight by its lines.</summary>
    public static bool IsReadable(string path) =>
        ReadableExtensions.Contains(Path.GetExtension(path).ToLowerInvariant())
        || SvmLightUtils.LooksLikeSvmLight(path);

    /// <summary>
    /// The data files directly inside <paramref name="directory"/> — those FilePrepper reads
    /// (<see cref="IsReadable"/>) — ordered by file name, ordinally. Ordinal because the order is the
    /// row order of the table they make, and a culture-aware comparison can ignore characters such as
    /// '-' and so put <c>a_1</c> before <c>a-2</c>. Sequence numbers sort right when they are padded,
    /// as exports that write a file per record pad them.
    /// </summary>
    public static IReadOnlyList<string> FilesIn(string directory) =>
        Directory.EnumerateFiles(directory)
            .Where(IsReadable)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// The field delimiter for a delimited-text file at this path: a tab for <c>.tsv</c>; for any other name the delimiter
    /// the file's own first lines prove (<c>;</c>, a tab or <c>|</c> — see <see cref="DelimiterSniffer"/>), else a comma.
    /// </summary>
    public static string DelimiterFor(string path) =>
        FromPath(path) == DataFileFormat.Tsv ? "\t" : DelimiterSniffer.For(path);
}
