namespace FilePrepper.Utils;

/// <summary>
/// Reads a delimited text file's own delimiter off its first lines, for a file that is not named for one
/// (<c>.tsv</c>): Excel in many locales writes <c>;</c> under the name <c>.csv</c>, a database export writes a tab or
/// <c>|</c> under <c>.csv</c> or <c>.txt</c>. Read as a comma table such a file is one column headed by its whole first
/// line, and every command that names a column then fails on "column not found" with the columns listed as one string.
/// </summary>
/// <remarks>
/// The comma is the answer unless the file proves another: the header holds none outside quotes, holds the candidate
/// at least once, and every sampled line holds it exactly as often. A comma table whose cells hold a semicolon is
/// untouched (its header has commas), and so is a one-column file (it would need its header and every row to hold the same
/// number of the same character). A blank line is no row and is skipped. Two candidates that both fit are not told apart — the comma stays, as it was.
/// </remarks>
internal static class DelimiterSniffer
{
    private static readonly char[] Candidates = [';', '\t', '|'];
    private const int SampledLines = 50;

    public static string For(string path)
    {
        try
        {
            return Sniff(ReadLines(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return ","; // not readable here: the reader that opens it for real says why
        }
    }

    /// <summary>The delimiter the <paramref name="lines"/> (the head of a file) prove, else a comma.</summary>
    internal static string Sniff(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
            return ",";

        var header = lines[0];
        if (CountOutsideQuotes(header, ',') > 0)
            return ",";

        var fitting = Candidates
            .Select(c => (Char: c, Count: CountOutsideQuotes(header, c)))
            .Where(c => c.Count > 0 && lines.Skip(1).Where(l => l.Length > 0).All(l => CountOutsideQuotes(l, c.Char) == c.Count))
            .ToList();
        return fitting.Count == 1 ? fitting[0].Char.ToString() : ",";
    }

    private static List<string> ReadLines(string path)
    {
        var lines = new List<string>(SampledLines);
        using var reader = CsvUtils.CreateReader(path);
        while (lines.Count < SampledLines && reader.ReadLine() is { } line)
        {
            if (line.Length > 0 || lines.Count > 0)
                lines.Add(line);
        }
        // A file that ends in blank lines has them as empty trailing lines; they carry no field.
        while (lines.Count > 1 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);
        return lines;
    }

    /// <summary>How often <paramref name="delimiter"/> occurs in <paramref name="line"/> outside a double-quoted span.</summary>
    private static int CountOutsideQuotes(string line, char delimiter)
    {
        var count = 0;
        var inQuotes = false;
        foreach (var ch in line)
        {
            if (ch == '"')
                inQuotes = !inQuotes;
            else if (ch == delimiter && !inQuotes)
                count++;
        }
        return count;
    }
}
