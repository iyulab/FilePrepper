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

    /// <summary>Reads <paramref name="text"/> as a date and time in the invariant culture, keeping the clock as written.</summary>
    public static bool TryParse(string? text, out DateTime value)
    {
        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, ReadStyles, out var parsed))
        {
            value = parsed.DateTime;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>Reads <paramref name="text"/> in exactly <paramref name="format"/>, in the invariant culture.</summary>
    public static bool TryParseExact(string? text, string format, out DateTime value) =>
        DateTime.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);

    /// <summary>Writes <paramref name="value"/> in <paramref name="format"/>, in the invariant culture: Gregorian calendar, ASCII digits, <c>:</c> and <c>/</c> as written.</summary>
    public static string Format(DateTime value, string format = DefaultFormat) =>
        value.ToString(format, CultureInfo.InvariantCulture);

    /// <summary>The ISO 8601 week of the year (a week starts on Monday; week 1 is the one with the year's first Thursday).</summary>
    public static int WeekOfYear(DateTime value) => ISOWeek.GetWeekOfYear(value);
}
