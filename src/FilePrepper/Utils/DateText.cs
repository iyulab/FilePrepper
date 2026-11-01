using System.Globalization;

namespace FilePrepper.Utils;

/// <summary>
/// Dates and times as a data file writes them — <c>2026-01-05 23:30</c>, whatever the machine's culture and time zone.
/// Every place FilePrepper reads a date from a cell or writes one into a cell goes through here, the way numbers go
/// through <see cref="NumberText"/>.
/// </summary>
/// <remarks>
/// They were read and written in the current culture. On a Thai machine the output format <c>yyyy-MM-dd</c> wrote the
/// Buddhist-era year (2569); where the time separator is not a colon, <c>HH:mm:ss</c> wrote <c>23.30.00</c>; a day-first
/// culture read <c>01/02/2026</c> as 1 February; and a value ending in <c>Z</c> or a UTC offset came back shifted into
/// the machine's own zone, so the hour of <c>23:30Z</c> was 8 on a UTC+9 machine. The clock is now read as written.
/// </remarks>
public static class DateText
{
    /// <summary>The format of a date and time written back without a format of its own.</summary>
    public const string DefaultFormat = "yyyy-MM-dd HH:mm:ss";

    // A value with no zone is taken as written; a value with a zone keeps the clock it was written with (DateTimeOffset.DateTime),
    // so neither depends on where the program runs.
    private const DateTimeStyles ReadStyles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces;

    /// <summary>The forms with no separators, which the general reader does not take: <c>20260102</c>, <c>202601020304</c>, <c>20260102030405</c>.</summary>
    private static readonly string[] CompactFormats = ["yyyyMMddHHmmss", "yyyyMMddHHmm", "yyyyMMdd"];

    /// <summary>Reads <paramref name="text"/> as a date and time in the invariant culture, keeping the clock as written.</summary>
    public static bool TryParse(string? text, out DateTime value) => TryParse(text, CultureInfo.InvariantCulture, out value);

    /// <summary>
    /// Reads <paramref name="text"/> as a date and time in <paramref name="culture"/> (day-first for a day-first culture), keeping the
    /// clock as written. This is the one reader of a free-form date: <c>extract-date</c> read with <c>DateTime.Parse</c> and a value ending
    /// in <c>Z</c> or a UTC offset came back in the machine's own zone, so <c>2026-01-02T23:30:00Z</c> was the 3rd, 8 o'clock on a UTC+9
    /// machine and the 2nd, 23 o'clock on any other; <c>datetime</c>, <c>merge-asof</c> and <c>extract-date</c> each took a different set of
    /// forms (only the first two took <c>20260102</c>).
    /// </summary>
    public static bool TryParse(string? text, IFormatProvider culture, out DateTime value)
    {
        if (DateTimeOffset.TryParse(text, culture, ReadStyles, out var parsed))
        {
            value = parsed.DateTime;
            return true;
        }

        foreach (var format in CompactFormats)
        {
            if (TryParseExact(text?.Trim(), format, CultureInfo.InvariantCulture, out value))
                return true;
        }

        value = default;
        return false;
    }

    /// <summary>Reads <paramref name="text"/> in exactly <paramref name="format"/>, in the invariant culture.</summary>
    public static bool TryParseExact(string? text, string format, out DateTime value) =>
        TryParseExact(text, format, CultureInfo.InvariantCulture, out value);

    /// <summary>Reads <paramref name="text"/> in exactly <paramref name="format"/>, in <paramref name="culture"/>, keeping the clock as written.</summary>
    public static bool TryParseExact(string? text, string format, IFormatProvider culture, out DateTime value)
    {
        if (DateTimeOffset.TryParseExact(text, format, culture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            value = parsed.DateTime;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>Writes <paramref name="value"/> in <paramref name="format"/>, in the invariant culture: Gregorian calendar, ASCII digits, <c>:</c> and <c>/</c> as written.</summary>
    public static string Format(DateTime value, string format = DefaultFormat) =>
        value.ToString(format, CultureInfo.InvariantCulture);

    /// <summary>The ISO 8601 week of the year (a week starts on Monday; week 1 is the one with the year's first Thursday).</summary>
    public static int WeekOfYear(DateTime value) => ISOWeek.GetWeekOfYear(value);
}
