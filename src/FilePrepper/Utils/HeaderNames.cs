namespace FilePrepper.Utils;

/// <summary>
/// Column names are how every task finds a column, and a row is held by name — so two columns of one name cannot both
/// be kept. Reading such a file used to succeed: the row's second value overwrote the first, the first column vanished
/// from the output, and every command reported success on a table that was no longer the one that was read.
/// </summary>
internal static class HeaderNames
{
    /// <summary>
    /// Throws when a file has no columns at all (an empty file, or a JSON list with no records): there is no table to
    /// act on. Nine commands used to write a one-line file whose header was the word <c>NoData</c> and report success,
    /// while twenty-two others failed on the same file with whatever their own code tripped on.
    /// </summary>
    public static void RefuseNone(IReadOnlyCollection<string> headers, string source)
    {
        if (headers.Count > 0)
            return;

        throw new InvalidDataException(
            $"{Path.GetFileName(source)} has no columns (the file is empty), so there is no table to read.");
    }

    /// <summary>Throws when two columns of <paramref name="headers"/> have the same name; says which and where.</summary>
    public static void RefuseRepeated(IEnumerable<string> headers, string source)
    {
        var repeated = headers
            .Select((name, index) => (name, position: index + 1))
            .GroupBy(column => column.name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"'{group.Key}' (columns {string.Join(", ", group.Select(column => column.position))})")
            .ToList();
        if (repeated.Count == 0)
            return;

        throw new InvalidDataException(
            $"{Path.GetFileName(source)} has columns of the same name: {string.Join("; ", repeated)}. " +
            "A column is found by its name, so only one of them could be kept; rename or remove the repeats in the source file first.");
    }
}
