using System.Text.RegularExpressions;

namespace FilePrepper.Pipeline;

/// <summary>
/// The window of a resample, <c>5T</c> (5 minutes), <c>1H</c> (1 hour) or <c>1D</c> (1 day): a whole number of
/// minutes, hours or days, at least one, small enough to be a time span. Read here once, for the option that
/// validates it and the pipeline that applies it.
/// </summary>
/// <remarks>
/// The unit is upper-case. <c>0T</c> used to pass the format check and then divide by zero while assigning rows to
/// windows, and a number too large for an <see cref="int"/> overflowed in the parse.
/// </remarks>
internal static partial class ResampleWindow
{
    internal const string Format = "Use a whole number and a unit like '5T' (5 minutes), '1H' (1 hour) or '1D' (1 day).";

    [GeneratedRegex(@"^(\d+)([THD])$", RegexOptions.CultureInvariant)]
    private static partial Regex Shape();

    /// <summary>Reads <paramref name="window"/>; on failure <paramref name="error"/> says why.</summary>
    internal static bool TryParse(string? window, out TimeSpan span, out string? error)
    {
        span = default;
        var match = Shape().Match(window ?? string.Empty);
        if (!match.Success)
        {
            error = $"Invalid window format: {window}. {Format}";
            return false;
        }

        var unit = match.Groups[2].Value switch
        {
            "T" => TimeSpan.TicksPerMinute,
            "H" => TimeSpan.TicksPerHour,
            _ => TimeSpan.TicksPerDay
        };

        if (!long.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var count)
            || count > TimeSpan.MaxValue.Ticks / unit)
        {
            error = $"Window {window} is too large to be a time span.";
            return false;
        }

        if (count == 0)
        {
            error = $"Window {window} is empty: it must be at least 1 minute, hour or day.";
            return false;
        }

        span = TimeSpan.FromTicks(count * unit);
        error = null;
        return true;
    }

    /// <summary>Whether the row has a time in <paramref name="column"/> that a data file can be read as.</summary>
    internal static bool HasReadableTime(IReadOnlyDictionary<string, string> row, string column) =>
        row.TryGetValue(column, out var text) && Utils.DateText.TryParse(text, out _);
}
