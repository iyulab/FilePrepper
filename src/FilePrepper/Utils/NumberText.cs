using System.Globalization;

namespace FilePrepper.Utils;

/// <summary>
/// Numbers as a data file writes them — <c>1.5</c>, whatever the machine's culture. Every place FilePrepper reads a
/// number from a cell or writes one into a cell goes through here.
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
        double.TryParse(text, Styles, CultureInfo.InvariantCulture, out value);

    /// <summary>Reads <paramref name="text"/> as a number in the invariant culture.</summary>
    /// <exception cref="FormatException">It is not a number.</exception>
    public static double Parse(string text) => double.Parse(text, Styles, CultureInfo.InvariantCulture);

    /// <summary>Reads <paramref name="text"/> as a decimal number in the invariant culture.</summary>
    public static bool TryParseDecimal(string? text, out decimal value) =>
        decimal.TryParse(text, Styles, CultureInfo.InvariantCulture, out value);

    /// <summary>Writes <paramref name="value"/> the way <see cref="TryParse"/> reads it.</summary>
    public static string Format(double value) => value.ToString("G", CultureInfo.InvariantCulture);
}
