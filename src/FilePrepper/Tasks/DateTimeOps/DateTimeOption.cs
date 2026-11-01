namespace FilePrepper.Tasks.DateTimeOps;

/// <summary>
/// Options of <see cref="DateTimeTask"/>: rewrite a date column in another format, convert an Excel serial number, or
/// split a date into numeric features. Dates are read and written as a data file writes them, whatever the machine's
/// culture or time zone (<see cref="Utils.DateText"/>): the Gregorian calendar, a month-first <c>01/02/2026</c>, and the clock
/// as written — a value ending in <c>Z</c> or a UTC offset keeps its own hour.
/// </summary>
public class DateTimeOption : SingleInputOption
{
    /// <summary>
    /// The column to read; required. <see cref="DateTimeMode.Parse"/> and <see cref="DateTimeMode.ParseExcel"/> overwrite it
    /// in place; a value that cannot be read (or an empty one) is left as it is and counted as failed in the log.
    /// </summary>
    public string Column { get; set; } = string.Empty;

    /// <summary>The operation to apply.</summary>
    public DateTimeMode Mode { get; set; }

    /// <summary>
    /// The exact format of the values for <see cref="DateTimeMode.Parse"/> (e.g. <c>yyyy-MM-dd HH:mm</c>, <c>yyyyMMddHHmm</c>);
    /// required in that mode. A value that does not match it completely is not parsed.
    /// </summary>
    public string? InputFormat { get; set; }

    /// <summary>
    /// The format the date is written in by <see cref="DateTimeMode.Parse"/> and <see cref="DateTimeMode.ParseExcel"/>;
    /// <c>yyyy-MM-dd HH:mm:ss</c> by default. Written with the invariant culture, so <c>:</c> and <c>/</c> stay as typed.
    /// </summary>
    public string OutputFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    /// The features <see cref="DateTimeMode.ExtractFeatures"/> adds as columns named <c>{Column}_{Feature}</c>, comma-separated:
    /// Year, Month, Day, Hour, Minute, DayOfWeek (Sunday 0), DayOfYear, WeekOfYear (ISO 8601), Quarter. Names are not
    /// case-sensitive; an unknown name is skipped with a warning. Required in that mode. The value is read in the formats
    /// <c>yyyy-MM-dd[ HH:mm[:ss]]</c>, <c>yyyy/MM/dd[ HH:mm[:ss]]</c>, <c>MM/dd/yyyy[ HH:mm[:ss]]</c>, <c>yyyyMMdd[HHmm[ss]]</c>,
    /// <c>M/d/yyyy H:mm</c>, then any other the invariant culture reads (ISO 8601 with a zone included).
    /// </summary>
    public string? Features { get; set; }

    /// <summary>Drop <see cref="Column"/> after its features are extracted; false by default.</summary>
    public bool RemoveOriginal { get; set; } = false;

    /// <inheritdoc />
    protected override string[] ValidateInternal()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Column))
        {
            errors.Add("Column name is required");
        }

        if (Mode == DateTimeMode.Parse && string.IsNullOrWhiteSpace(InputFormat))
        {
            errors.Add("InputFormat is required for Parse mode");
        }

        if (Mode == DateTimeMode.ExtractFeatures && string.IsNullOrWhiteSpace(Features))
        {
            errors.Add("Features are required for ExtractFeatures mode");
        }

        return errors.ToArray();
    }
}

/// <summary>The operation <see cref="DateTimeTask"/> applies.</summary>
public enum DateTimeMode
{
    /// <summary>Read values in <see cref="DateTimeOption.InputFormat"/> and write them in <see cref="DateTimeOption.OutputFormat"/>.</summary>
    Parse,

    /// <summary>Turn an Excel serial number (days since 1899-12-30, a fraction is the time of day) into a date in <see cref="DateTimeOption.OutputFormat"/>.</summary>
    ParseExcel,

    /// <summary>Add one column per feature of <see cref="DateTimeOption.Features"/>.</summary>
    ExtractFeatures
}
