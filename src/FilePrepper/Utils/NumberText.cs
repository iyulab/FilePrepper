using System.Globalization;

namespace FilePrepper.Utils;

/// <summary>
/// Numbers as a data file writes them — <c>1.5</c>, whatever the machine's culture. Every place FilePrepper reads a
/// number from a cell or writes one into a cell goes through here: a cell is a number to every command or to none.
/// A number is finite and written without separators: <c>NaN</c>, <c>Infinity</c> and a value too large for a double
/// (<c>1e400</c>) are not numbers, and <c>1,200</c> is not one either — whether a comma separates thousands or
/// decimals is a fact about the file's locale that a reader cannot know (<c>1,234</c> is 1234 or 1.234), so the
/// <c>clean</c> command is where the file says which.
/// </summary>
/// <remarks>
/// They were read and written in the current culture, so on a comma-decimal machine (de-DE) the cells <c>1.5</c> and
/// <c>2.5</c> were read as 15 and 25 — a mean of 20 — and a computed 0.5 was written back as <c>0,5</c>, which the next
/// reader of the file split into two fields.
/// </remarks>
public static class NumberText
{
    private const NumberStyles Styles = NumberStyles.Float;

    /// <summary>Reads <paramref name="text"/> as a number in the invariant culture.</summary>
    public static bool TryParse(string? text, out double value) =>
        double.TryParse(text, Styles, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    /// <summary>Reads <paramref name="text"/> as a number in the invariant culture.</summary>
    /// <exception cref="FormatException">It is not a number.</exception>
    public static double Parse(string text) =>
        TryParse(text, out var value) ? value : throw new FormatException($"'{text}' is not a number.");

    /// <summary>Reads <paramref name="text"/> as a decimal number in the invariant culture.</summary>
    public static bool TryParseDecimal(string? text, out decimal value) =>
        decimal.TryParse(text, Styles, CultureInfo.InvariantCulture, out value);

    /// <summary>Writes <paramref name="value"/> the way <see cref="TryParse"/> reads it.</summary>
    public static string Format(double value) => value.ToString("G", CultureInfo.InvariantCulture);
}
