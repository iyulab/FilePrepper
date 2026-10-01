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
    /// <summary>Extensions FilePrepper can read, lower-case with the dot.</summary>
    public static readonly IReadOnlyList<string> ReadableExtensions =
        [".csv", ".tsv", ".json", ".xlsx", ".xls", ".parquet", ".svm", ".svmlight", ".libsvm"];

    /// <summary>
    /// The format of <paramref name="path"/> by its extension. An extension that names no format is
    /// read as CSV, as delimited text usually is whatever it is called — unless the file's lines are
    /// SVMlight (<c>label index:value …</c>), which learning-to-rank sets publish under names such as
    /// <c>rank.train</c> and which read as CSV would be one column headed by its first row.
    /// </summary>
    /// <exception cref="NotSupportedException">The file is XML, which FilePrepper writes but does
    /// not read — read as delimited text it would come back as nonsense rather than an error.</exception>
    public static DataFileFormat FromPath(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".xml" => throw new NotSupportedException(
                $"XML is an output format only; FilePrepper reads {string.Join(", ", ReadableExtensions)}. ({path})"),
            ".tsv" => DataFileFormat.Tsv,
            ".json" => DataFileFormat.Json,
            ".xlsx" or ".xls" => DataFileFormat.Excel,
            ".parquet" => DataFileFormat.Parquet,
            ".svm" or ".svmlight" or ".libsvm" => DataFileFormat.SvmLight,
            _ => SvmLightUtils.LooksLikeSvmLight(path) ? DataFileFormat.SvmLight : DataFileFormat.Csv
        };

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

    /// <summary>The field delimiter for a delimited-text file at this path.</summary>
    public static string DelimiterFor(string path) =>
        FromPath(path) == DataFileFormat.Tsv ? "\t" : ",";
}
