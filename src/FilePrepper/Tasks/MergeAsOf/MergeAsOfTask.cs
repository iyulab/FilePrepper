using System.Globalization;

namespace FilePrepper.Tasks.MergeAsOf;

/// <summary>
/// Joins each row of the left file with the one row of the right file whose time is nearest in the direction asked, as
/// pandas' <c>merge_asof</c> does.
/// </summary>
/// <remarks>
/// <para>
/// Among right rows with the same time, <see cref="AsOfDirection.Backward"/> takes the last in file order and
/// <see cref="AsOfDirection.Forward"/> the first; <see cref="AsOfDirection.Nearest"/> takes the closer of the two, and
/// the earlier one when they are equally close. The right file need not be sorted. A left row with no match (nothing in
/// the direction, or farther than the tolerance, or no readable time) is kept with empty right columns. The right file's
/// matching column is not copied - its value is the time the match was found at; a column of the right file that has the
/// name of a column of the left file gets the suffix.
/// </para>
/// <para>A time that cannot be read is not matched: a left row keeps its place, a right row cannot be chosen; both are counted in a warning.</para>
/// </remarks>
public class MergeAsOfTask : BaseTask<MergeAsOfOption>
{
    private List<Dictionary<string, string>> _rightRecords = new();
    private List<string> _rightHeaders = new();

    public MergeAsOfTask(ILogger<MergeAsOfTask> logger) : base(logger) { }

    protected override async Task<List<Dictionary<string, string>>> PreProcessRecordsAsync(
        List<Dictionary<string, string>> records)
    {
        // Read the right (secondary) file, in whatever format it is
        var rightPath = Options.InputPaths[1];
        (_rightRecords, _rightHeaders) = await ReadInputAsync(rightPath);

        _logger.LogInformation(
            "Loaded right file: {Count} records with {ColumnCount} columns",
            _rightRecords.Count, _rightHeaders.Count);

        return records; // Return left records unchanged
    }

    protected override async Task<List<Dictionary<string, string>>> ProcessRecordsAsync(
        List<Dictionary<string, string>> leftRecords)
    {
        _logger.LogInformation("Starting merge_asof operation");
        _logger.LogInformation("  Direction: {Direction}", Options.Direction);
        _logger.LogInformation("  Left column: {LeftCol}", Options.LeftOnColumn);
        _logger.LogInformation("  Right column: {RightCol}", Options.RightOnColumn);

        if (Options.Tolerance.HasValue)
        {
            _logger.LogInformation("  Tolerance: {Tolerance} seconds", Options.Tolerance.Value);
        }

        // With no left rows the output is the header alone - all of it, the right file's columns too.
        _originalHeaders = MergedHeaders();

        // The right file's times, in order (ties in file order), for a binary search per left row
        var rightSorted = ParseAndSortRight();

        var result = new List<Dictionary<string, string>>();
        int matchCount = 0;
        int noMatchCount = 0;
        var leftUnreadable = new List<(int Row, string Value)>();

        for (var row = 0; row < leftRecords.Count; row++)
        {
            var leftRecord = leftRecords[row];
            var leftValueStr = leftRecord.GetValueOrDefault(Options.LeftOnColumn, string.Empty);
            if (!TryParseDateTime(leftValueStr, out var leftValue))
            {
                // A time that cannot be read matches nothing; the row keeps its place with empty right columns
                leftUnreadable.Add((row + 1, leftValueStr));
                result.Add(CreateUnmatchedRecord(leftRecord));
                noMatchCount++;
                continue;
            }

            var match = FindMatch(leftValue, rightSorted);
            if (match is { } index)
            {
                result.Add(MergeRecords(leftRecord, _rightRecords[index]));
                matchCount++;
            }
            else
            {
                result.Add(CreateUnmatchedRecord(leftRecord));
                noMatchCount++;
            }
        }

        if (leftUnreadable.Count > 0)
        {
            _logger.LogWarning(
                "{Count} of {Total} left row(s) have no readable time in column '{Column}' and match nothing (first: data row {Row}, value '{Value}')",
                leftUnreadable.Count, leftRecords.Count, Options.LeftOnColumn, leftUnreadable[0].Row, leftUnreadable[0].Value);
        }

        _logger.LogInformation(
            "✓ merge_asof complete: {MatchCount} matches, {NoMatchCount} no matches, {TotalCount} total records",
            matchCount, noMatchCount, result.Count);

        return await Task.FromResult(result);
    }

    /// <summary>The columns of the merged table: the left file's, then the right file's but its matching column.</summary>
    private List<string> MergedHeaders()
    {
        var merged = new List<string>(_originalHeaders);
        foreach (var header in _rightHeaders.Where(header => header != Options.RightOnColumn))
        {
            merged.Add(merged.Contains(header, StringComparer.Ordinal) ? $"{header}{Options.Suffix}" : header);
        }

        return merged;
    }

    private (DateTime Value, int Index)[] ParseAndSortRight()
    {
        var parsed = new List<(DateTime Value, int Index)>();
        var unreadable = new List<(int Row, string Value)>();

        for (int i = 0; i < _rightRecords.Count; i++)
        {
            var valueStr = _rightRecords[i].GetValueOrDefault(Options.RightOnColumn, string.Empty);
            if (TryParseDateTime(valueStr, out var value))
            {
                parsed.Add((value, i));
            }
            else
            {
                unreadable.Add((i + 1, valueStr));
            }
        }

        // By time, and by position in the file among equal times: List.Sort is not stable, and which of several rows with
        // the same time was matched must not depend on it.
        parsed.Sort((a, b) => a.Value != b.Value ? a.Value.CompareTo(b.Value) : a.Index.CompareTo(b.Index));

        if (unreadable.Count > 0)
        {
            _logger.LogWarning(
                "{Count} of {Total} right row(s) have no readable time in column '{Column}' and cannot be matched (first: data row {Row}, value '{Value}')",
                unreadable.Count, _rightRecords.Count, Options.RightOnColumn, unreadable[0].Row, unreadable[0].Value);
        }

        _logger.LogInformation(
            "Parsed and sorted {Count} valid datetime records from right file",
            parsed.Count);

        return [.. parsed];
    }

    /// <summary>The index in the right file of the row matched to <paramref name="leftValue"/>, or null.</summary>
    private int? FindMatch(DateTime leftValue, (DateTime Value, int Index)[] sorted)
    {
        // First position whose time is after leftValue; the one before it is the last time <= leftValue
        var upper = UpperBound(sorted, leftValue);
        var backward = upper > 0 ? sorted[upper - 1] : ((DateTime, int)?)null;

        // First position whose time is >= leftValue
        var lower = LowerBound(sorted, leftValue);
        var forward = lower < sorted.Length ? sorted[lower] : ((DateTime, int)?)null;

        (DateTime Value, int Index)? chosen = Options.Direction switch
        {
            AsOfDirection.Backward => backward,
            AsOfDirection.Forward => forward,
            _ => Closer(leftValue, backward, forward)
        };

        return chosen is { } match && IsWithinTolerance(leftValue, match.Value) ? match.Index : null;
    }

    private static (DateTime Value, int Index)? Closer(
        DateTime leftValue,
        (DateTime Value, int Index)? backward,
        (DateTime Value, int Index)? forward)
    {
        if (backward is null || forward is null)
            return backward ?? forward;

        // Equally close: the earlier time (what an as-of join looks back to)
        return forward.Value.Value - leftValue < leftValue - backward.Value.Value ? forward : backward;
    }

    private static int UpperBound((DateTime Value, int Index)[] sorted, DateTime value)
    {
        int low = 0, high = sorted.Length;
        while (low < high)
        {
            var mid = low + (high - low) / 2;
            if (sorted[mid].Value <= value) low = mid + 1; else high = mid;
        }

        return low;
    }

    private static int LowerBound((DateTime Value, int Index)[] sorted, DateTime value)
    {
        int low = 0, high = sorted.Length;
        while (low < high)
        {
            var mid = low + (high - low) / 2;
            if (sorted[mid].Value < value) low = mid + 1; else high = mid;
        }

        return low;
    }

    private bool IsWithinTolerance(DateTime leftValue, DateTime rightValue)
    {
        if (!Options.Tolerance.HasValue)
            return true;

        var diff = Math.Abs((leftValue - rightValue).TotalSeconds);
        return diff <= Options.Tolerance.Value;
    }

    private Dictionary<string, string> MergeRecords(
        Dictionary<string, string> leftRecord,
        Dictionary<string, string> rightRecord)
    {
        var merged = new Dictionary<string, string>(leftRecord);

        // Add all right columns with suffix if needed
        foreach (var kvp in rightRecord)
        {
            // Skip the matching column from right file
            if (kvp.Key == Options.RightOnColumn)
                continue;

            // Add with suffix if column already exists in left
            var columnName = merged.ContainsKey(kvp.Key)
                ? $"{kvp.Key}{Options.Suffix}"
                : kvp.Key;

            merged[columnName] = kvp.Value;
        }

        return merged;
    }

    private Dictionary<string, string> CreateUnmatchedRecord(Dictionary<string, string> leftRecord)
    {
        var result = new Dictionary<string, string>(leftRecord);

        // Add empty columns for all right columns (except matching column)
        foreach (var header in _rightHeaders)
        {
            if (header == Options.RightOnColumn)
                continue;

            var columnName = result.ContainsKey(header)
                ? $"{header}{Options.Suffix}"
                : header;

            result[columnName] = string.Empty;
        }

        return result;
    }

    private static bool TryParseDateTime(string value, out DateTime result) => DateText.TryParse(value, out result);

    protected override IEnumerable<string> GetRequiredColumns() => Array.Empty<string>();

    protected override string[] ValidateTaskSpecific(TaskContext context)
    {
        // Each matching column is a column of its own file - checked against the headers, so an empty file is not mistaken for a missing column
        var errors = new List<string>();
        if (Options.InputPaths.Count != 2 || Options.InputPaths.Any(path => !File.Exists(path)))
            return [];

        var left = ReadInputAsync(Options.InputPaths[0]).GetAwaiter().GetResult().headers;
        var right = ReadInputAsync(Options.InputPaths[1]).GetAwaiter().GetResult().headers;

        if (!left.Contains(Options.LeftOnColumn, StringComparer.Ordinal))
            errors.Add($"Left column '{Options.LeftOnColumn}' not found in the left file (columns: {string.Join(", ", left.Take(20))})");

        if (!right.Contains(Options.RightOnColumn, StringComparer.Ordinal))
            errors.Add($"Right column '{Options.RightOnColumn}' not found in the right file (columns: {string.Join(", ", right.Take(20))})");

        return [.. errors];
    }
}
