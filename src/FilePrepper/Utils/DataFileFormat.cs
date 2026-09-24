namespace FilePrepper.Utils;

/// <summary>The formats FilePrepper reads data from.</summary>
public enum DataFileFormat
{
    Csv,
    Tsv,
    Json,
    Excel,
    Parquet
}

/// <summary>
/// Which reader a data file gets — decided here once, so a command, a task and the pipeline
/// cannot disagree about what a file is.
/// </summary>
public static class DataFileFormats
{
    /// <summary>Extensions FilePrepper can read, lower-case with the dot.</summary>
    public static readonly IReadOnlyList<string> ReadableExtensions =
        [".csv", ".tsv", ".json", ".xlsx", ".xls", ".parquet"];

    /// <summary>
    /// The format of <paramref name="path"/> by its extension. An extension that names no other
    /// format is read as CSV, as delimited text usually is whatever it is called.
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
            _ => DataFileFormat.Csv
        };

    /// <summary>Whether <paramref name="path"/> has an extension FilePrepper reads.</summary>
    public static bool IsReadable(string path) =>
        ReadableExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    /// <summary>The field delimiter for a delimited-text file at this path.</summary>
    public static string DelimiterFor(string path) =>
        FromPath(path) == DataFileFormat.Tsv ? "\t" : ",";
}
