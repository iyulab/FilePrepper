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

        // Records, not lines: a quoted cell may hold a line break, and the lines of such a record each hold fewer delimiters
        // than the record does. Where a quote opens depends on the delimiter (it opens at the start of a field), so the
        // records are cut once for each delimiter tried.
        var commaRecords = Records(lines, ',');
        if (commaRecords.Count == 0 || CountOutsideQuotes(commaRecords[0], ',') > 0)
            return ",";

        var fitting = new List<(char Char, int Count)>();
        foreach (var candidate in Candidates)
        {
            var records = Records(lines, candidate);
            if (records.Count == 0)
                continue;
            var count = CountOutsideQuotes(records[0], candidate);
            if (count > 0 && records.Skip(1).Where(r => r.Length > 0).All(r => CountOutsideQuotes(r, candidate) == count))
                fitting.Add((candidate, count));
        }
        return fitting.Count == 1 ? fitting[0].Char.ToString() : ",";
    }

    /// <summary>
    /// The records the <paramref name="lines"/> hold: a line that ends inside a quoted field is joined to the next one. A record
    /// the sample cuts off before its quote closes is dropped — its count would be partial.
    /// </summary>
    private static List<string> Records(IReadOnlyList<string> lines, char delimiter)
    {
        var records = new List<string>();
        var current = new System.Text.StringBuilder();
        var open = false;
        foreach (var line in lines)
        {
            if (open)
                current.Append('\n');
            current.Append(line);
            CountOutsideQuotes(current.ToString(), delimiter, out open);
            if (open)
                continue;
            records.Add(current.ToString());
            current.Clear();
        }
        return records;
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

    /// <summary>
    /// How often <paramref name="delimiter"/> occurs in <paramref name="line"/> outside a double-quoted field. A quote opens a quoted
    /// field only at the start of a field, as a CSV reader reads it: a quote inside an unquoted value (<c>12" pipe</c>) is text, and
    /// treating it as an opening hid every delimiter after it, so a semicolon file with an inch mark in one cell was not recognised.
    /// Inside a quoted field a doubled quote is a quote and a single one closes it.
    /// </summary>
    private static int CountOutsideQuotes(string line, char delimiter) => CountOutsideQuotes(line, delimiter, out _);

    /// <summary>As above, and whether the text ends inside a quoted field (<paramref name="endsInQuotes"/>).</summary>
    private static int CountOutsideQuotes(string line, char delimiter, out bool endsInQuotes)
    {
        var count = 0;
        var inQuotes = false;
        var atFieldStart = true;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                        i++;
                    else
                        inQuotes = false;
                }
                continue;
            }

            if (ch == '"' && atFieldStart)
                inQuotes = true;
            else if (ch == delimiter)
                count++;
            atFieldStart = ch == delimiter;
        }
        endsInQuotes = inQuotes;
        return count;
    }
}
