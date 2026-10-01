using System.Globalization;
using System.Text.RegularExpressions;

namespace FilePrepper.Utils;

/// <summary>
/// Reads SVMlight / libsvm text — <c>label [qid:q] index:value … [# comment]</c>, one row per line,
/// the format learning-to-rank benchmarks and <c>sklearn.datasets.load_svmlight_file</c> use.
/// </summary>
/// <remarks>
/// <para>The table has a <c>label</c> column, a <c>qid</c> column when the rows are grouped, and one
/// <c>f&lt;index&gt;</c> column per feature index that occurs anywhere in the file, in index order. A
/// feature a line leaves out is <c>0</c> — the format is sparse, and an absent pair means zero, not
/// unknown. Values are kept as written.</para>
/// <para>Groups come from <c>qid:</c> on each line, or — when no line carries one — from a group-size
/// file beside the data (<c>&lt;file&gt;.query</c>, LightGBM's name, or <c>&lt;file&gt;.group</c>,
/// XGBoost's): one count per line, the rows of each consecutive group. The groups are then numbered
/// from 0.</para>
/// </remarks>
public static partial class SvmLightUtils
{
    /// <summary>The group-size files read beside a data file without <c>qid:</c>, in the order tried.</summary>
    public static readonly IReadOnlyList<string> GroupFileSuffixes = [".query", ".group"];

    [GeneratedRegex(@"^\s*[+-]?(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?(\s+qid:\S+)?(\s+\d+:\S+)+\s*(#.*)?$")]
    private static partial Regex Line();

    /// <summary>How many data lines <see cref="LooksLikeSvmLight"/> inspects.</summary>
    private const int SniffLines = 5;

    /// <summary>
    /// Whether the file at <paramref name="path"/> reads as SVMlight: its first data lines (comment and
    /// blank lines skipped) are all a number followed by <c>index:value</c> pairs. Delimited text never
    /// has that shape, so a file whose extension names no format can be told apart by its lines.
    /// </summary>
    public static bool LooksLikeSvmLight(string path)
    {
        try
        {
            if (!File.Exists(path)) return false;
            var seen = 0;
            foreach (var line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#')) continue;
                if (!Line().IsMatch(line)) return false;
                if (++seen == SniffLines) break;
            }
            return seen > 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Reads <paramref name="filePath"/> as a table — see the type's remarks for its columns.</summary>
    /// <exception cref="InvalidDataException">A token is not <c>index:value</c>, or a group-size file
    /// does not cover the rows exactly.</exception>
    public static async Task<(List<Dictionary<string, string>> records, List<string> headers)> ReadSvmLightFileAsync(
        string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"SVMlight file not found: {filePath}");

        var rows = new List<(string Label, string? Qid, Dictionary<int, string> Features)>();
        var indices = new SortedSet<int>();
        var lineNumber = 0;
        foreach (var raw in await File.ReadAllLinesAsync(filePath, cancellationToken).ConfigureAwait(false))
        {
            lineNumber++;
            var hash = raw.IndexOf('#');
            var line = (hash >= 0 ? raw[..hash] : raw).Trim();
            if (line.Length == 0) continue;

            var tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            string? qid = null;
            var features = new Dictionary<int, string>();
            foreach (var token in tokens.Skip(1))
            {
                var colon = token.IndexOf(':');
                if (colon <= 0)
                    throw new InvalidDataException($"Line {lineNumber} of {Path.GetFileName(filePath)}: '{token}' is not index:value.");
                var key = token[..colon];
                var value = token[(colon + 1)..];
                if (key == "qid")
                {
                    qid = value;
                }
                else if (int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                {
                    features[index] = value;
                    indices.Add(index);
                }
                else
                {
                    throw new InvalidDataException($"Line {lineNumber} of {Path.GetFileName(filePath)}: '{key}' is not a feature index.");
                }
            }
            rows.Add((tokens[0], qid, features));
        }

        var hasQid = rows.Any(r => r.Qid is not null);
        var groups = hasQid ? null : ReadGroupFile(filePath, rows.Count);
        var grouped = hasQid || groups is not null;

        var headers = new List<string> { "label" };
        if (grouped) headers.Add("qid");
        headers.AddRange(indices.Select(Name));

        var records = new List<Dictionary<string, string>>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            var (label, qid, features) = rows[i];
            var record = new Dictionary<string, string>(headers.Count) { ["label"] = label };
            if (grouped)
                record["qid"] = qid ?? groups?[i] ?? string.Empty;
            foreach (var index in indices)
                record[Name(index)] = features.TryGetValue(index, out var value) ? value : "0";
            records.Add(record);
        }
        return (records, headers);

        static string Name(int index) => "f" + index.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>The group number of each row, from a group-size file beside the data, or null when there is none.</summary>
    private static string[]? ReadGroupFile(string filePath, int rowCount)
    {
        var groupFile = GroupFileSuffixes.Select(s => filePath + s).FirstOrDefault(File.Exists);
        if (groupFile is null) return null;

        var sizes = File.ReadLines(groupFile)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => int.Parse(l.Trim(), NumberStyles.None, CultureInfo.InvariantCulture))
            .ToList();
        var total = sizes.Sum();
        if (total != rowCount)
            throw new InvalidDataException(
                $"{Path.GetFileName(groupFile)} groups {total} rows, but {Path.GetFileName(filePath)} has {rowCount}.");

        var groups = new string[rowCount];
        var row = 0;
        for (var g = 0; g < sizes.Count; g++)
            for (var k = 0; k < sizes[g]; k++)
                groups[row++] = g.ToString(CultureInfo.InvariantCulture);
        return groups;
    }
}
