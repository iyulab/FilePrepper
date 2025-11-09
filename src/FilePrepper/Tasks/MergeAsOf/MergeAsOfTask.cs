using System.Globalization;

namespace FilePrepper.Tasks.MergeAsOf;

public class MergeAsOfTask : BaseTask<MergeAsOfOption>
{
    private List<Dictionary<string, string>> _rightRecords = new();
    private List<string> _rightHeaders = new();

    public MergeAsOfTask(ILogger<MergeAsOfTask> logger) : base(logger) { }

    protected override async Task<List<Dictionary<string, string>>> PreProcessRecordsAsync(
        List<Dictionary<string, string>> records)
    {
        // Read the right (secondary) file
        var rightPath = Options.InputPaths[1];
        (_rightRecords, _rightHeaders) = await ReadCsvFileAsync(rightPath);

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

        // Validate columns exist
        if (!leftRecords.Any() || !leftRecords.First().ContainsKey(Options.LeftOnColumn))
        {
            throw new ValidationException(
                $"Left column '{Options.LeftOnColumn}' not found in left file",
                ValidationExceptionErrorCode.General);
        }

        if (!_rightRecords.Any() || !_rightRecords.First().ContainsKey(Options.RightOnColumn))
        {
            throw new ValidationException(
                $"Right column '{Options.RightOnColumn}' not found in right file",
                ValidationExceptionErrorCode.General);
        }

        // Parse and sort right records by the matching column
        var rightParsed = ParseAndSortRight();

        // Perform merge_asof
        var result = new List<Dictionary<string, string>>();
        int matchCount = 0;
        int noMatchCount = 0;

        foreach (var leftRecord in leftRecords)
        {
            var leftValueStr = leftRecord[Options.LeftOnColumn];
            if (!TryParseDateTime(leftValueStr, out var leftValue))
            {
                // If parsing fails, add record with empty right columns
                result.Add(CreateUnmatchedRecord(leftRecord));
                noMatchCount++;
                continue;
            }

            // Find matching record(s) in right file
            var matches = FindMatches(leftValue, rightParsed);

            if (matches.Any())
            {
                if (Options.AllowMultipleMatches)
                {
                    foreach (var match in matches)
                    {
                        result.Add(MergeRecords(leftRecord, _rightRecords[match.Index]));
                    }
                    matchCount += matches.Count;
                }
                else
                {
                    // Use only the best match
                    result.Add(MergeRecords(leftRecord, _rightRecords[matches[0].Index]));
                    matchCount++;
                }
            }
            else
            {
                result.Add(CreateUnmatchedRecord(leftRecord));
                noMatchCount++;
            }
        }

        _logger.LogInformation(
            "✓ merge_asof complete: {MatchCount} matches, {NoMatchCount} no matches, {TotalCount} total records",
            matchCount, noMatchCount, result.Count);

        return await Task.FromResult(result);
    }

    private List<(DateTime Value, int Index)> ParseAndSortRight()
    {
        var parsed = new List<(DateTime Value, int Index)>();

        for (int i = 0; i < _rightRecords.Count; i++)
        {
            var valueStr = _rightRecords[i][Options.RightOnColumn];
            if (TryParseDateTime(valueStr, out var value))
            {
                parsed.Add((value, i));
            }
        }

        // Sort by datetime value for efficient searching
        parsed.Sort((a, b) => a.Value.CompareTo(b.Value));

        _logger.LogInformation(
            "Parsed and sorted {Count} valid datetime records from right file",
            parsed.Count);

        return parsed;
    }

    private List<(DateTime Value, int Index)> FindMatches(
        DateTime leftValue,
        List<(DateTime Value, int Index)> rightParsed)
    {
        var matches = new List<(DateTime Value, int Index)>();

        if (Options.Direction == AsOfDirection.Backward)
        {
            // Find most recent value <= leftValue
            var match = rightParsed
                .Where(r => r.Value <= leftValue)
                .OrderByDescending(r => r.Value)
                .FirstOrDefault();

            if (match != default && IsWithinTolerance(leftValue, match.Value))
            {
                matches.Add(match);
            }
        }
        else if (Options.Direction == AsOfDirection.Forward)
        {
            // Find nearest future value >= leftValue
            var match = rightParsed
                .Where(r => r.Value >= leftValue)
                .OrderBy(r => r.Value)
                .FirstOrDefault();

            if (match != default && IsWithinTolerance(leftValue, match.Value))
            {
                matches.Add(match);
            }
        }
        else // Nearest
        {
            // Find closest value regardless of direction
            var match = rightParsed
                .OrderBy(r => Math.Abs((r.Value - leftValue).TotalSeconds))
                .FirstOrDefault();

            if (match != default && IsWithinTolerance(leftValue, match.Value))
            {
                matches.Add(match);
            }
        }

        return matches;
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

    private bool TryParseDateTime(string value, out DateTime result)
    {
        // Try multiple datetime formats
        var formats = new[]
        {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-dd",
            "yyyyMMddHHmmss",
            "yyyyMMddHHmm",
            "yyyyMMdd",
            "yyyy/MM/dd HH:mm:ss",
            "yyyy/MM/dd HH:mm",
            "yyyy/MM/dd",
            "MM/dd/yyyy HH:mm:ss",
            "MM/dd/yyyy HH:mm",
            "MM/dd/yyyy"
        };

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(
                value,
                format,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out result))
            {
                return true;
            }
        }

        // Try general datetime parsing
        if (DateTime.TryParse(value, out result))
        {
            return true;
        }

        result = default;
        return false;
    }

    protected override IEnumerable<string> GetRequiredColumns() => Array.Empty<string>();

    protected override string[] ValidateTaskSpecific(TaskContext context)
    {
        return Array.Empty<string>();
    }
}
