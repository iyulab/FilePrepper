namespace FilePrepper.Tasks.StringOps;

/// <summary>
/// Options of <see cref="StringTask"/>: one text operation (<see cref="Mode"/>) applied to a column of every record.
/// Which of the other properties apply depends on the mode; a property that does not apply to the chosen mode is ignored.
/// </summary>
public class StringOption : SingleInputOption
{
    /// <summary>The operation to apply.</summary>
    public StringMode Mode { get; set; }

    // Common

    /// <summary>
    /// The column the operation reads. Required by every mode except <see cref="StringMode.Concat"/>, which reads
    /// <see cref="Columns"/> instead.
    /// </summary>
    public string Column { get; set; } = string.Empty;

    /// <summary>
    /// The column the result is written to. Required by <see cref="StringMode.Concat"/>. When unset, a substring is
    /// written to <c>{Column}_substring</c> and every other mode overwrites <see cref="Column"/>.
    /// </summary>
    public string? OutputColumn { get; set; }

    // Substring

    /// <summary>The zero-based position the substring starts at; must not be negative. A start at or past the end of a value gives an empty string.</summary>
    public int StartIndex { get; set; }

    /// <summary>
    /// The longest substring to take, in characters; must not be negative. When unset the substring runs to the end of the value,
    /// and a value shorter than this gives what there is.
    /// </summary>
    public int? Length { get; set; }

    // Concat

    /// <summary>
    /// The columns <see cref="StringMode.Concat"/> joins, comma-separated, in this order. A name that is not a column of the
    /// record contributes an empty string.
    /// </summary>
    public string? Columns { get; set; }

    /// <summary>The text placed between the joined values of <see cref="StringMode.Concat"/>; empty by default.</summary>
    public string Separator { get; set; } = "";

    // Replace

    /// <summary>
    /// The text <see cref="StringMode.Replace"/> looks for, matched exactly (case-sensitive, not a pattern). Required and not
    /// empty, but whitespace is a valid value: <c>" "</c> replaces every space.
    /// </summary>
    public string? OldValue { get; set; }

    /// <summary>The text that takes the place of <see cref="OldValue"/>; unset replaces it with nothing.</summary>
    public string? NewValue { get; set; }

    // Trim

    /// <summary>Which end <see cref="StringMode.Trim"/> strips whitespace from; both by default.</summary>
    public TrimMode TrimMode { get; set; } = TrimMode.Both;

    protected override string[] ValidateInternal()
    {
        var errors = new List<string>();

        if (Mode == StringMode.Substring)
        {
            if (string.IsNullOrWhiteSpace(Column))
                errors.Add("Column is required for substring mode");
            if (StartIndex < 0)
                errors.Add("StartIndex must be >= 0");
            if (Length < 0)
                errors.Add("Length must be >= 0");
        }
        else if (Mode == StringMode.Concat)
        {
            if (string.IsNullOrWhiteSpace(Columns))
                errors.Add("Columns are required for concat mode (comma-separated)");
            if (string.IsNullOrWhiteSpace(OutputColumn))
                errors.Add("OutputColumn is required for concat mode");
        }
        else if (Mode == StringMode.Replace)
        {
            if (string.IsNullOrWhiteSpace(Column))
                errors.Add("Column is required for replace mode");
            // Whitespace is a legitimate thing to replace (" " → "_"); only the empty string is not, since it matches everywhere.
            if (string.IsNullOrEmpty(OldValue))
                errors.Add("OldValue is required for replace mode");
        }
        else if (Mode == StringMode.Trim || Mode == StringMode.Upper || Mode == StringMode.Lower)
        {
            if (string.IsNullOrWhiteSpace(Column))
                errors.Add($"Column is required for {Mode.ToString().ToLowerInvariant()} mode");
        }

        return errors.ToArray();
    }
}

/// <summary>The operation <see cref="StringTask"/> applies.</summary>
public enum StringMode
{
    /// <summary>Take part of the value, from <see cref="StringOption.StartIndex"/> for up to <see cref="StringOption.Length"/> characters.</summary>
    Substring,

    /// <summary>Join the values of <see cref="StringOption.Columns"/> with <see cref="StringOption.Separator"/>.</summary>
    Concat,

    /// <summary>Replace every occurrence of <see cref="StringOption.OldValue"/> with <see cref="StringOption.NewValue"/>.</summary>
    Replace,

    /// <summary>Strip whitespace from the ends chosen by <see cref="StringOption.TrimMode"/>.</summary>
    Trim,

    /// <summary>Upper-case the value; the same letters in every culture (invariant).</summary>
    Upper,

    /// <summary>Lower-case the value; the same letters in every culture (invariant).</summary>
    Lower
}

/// <summary>Which end of a value <see cref="StringMode.Trim"/> strips.</summary>
public enum TrimMode
{
    /// <summary>Both the start and the end.</summary>
    Both,

    /// <summary>The start only.</summary>
    Start,

    /// <summary>The end only.</summary>
    End
}
