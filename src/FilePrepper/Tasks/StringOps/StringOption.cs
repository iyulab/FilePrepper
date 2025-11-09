namespace FilePrepper.Tasks.StringOps;

public class StringOption : SingleInputOption
{
    public StringMode Mode { get; set; }

    // Common
    public string Column { get; set; } = string.Empty;
    public string? OutputColumn { get; set; }

    // Substring
    public int StartIndex { get; set; }
    public int? Length { get; set; }

    // Concat
    public string? Columns { get; set; } // comma-separated
    public string Separator { get; set; } = "";

    // Replace
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }

    // Trim
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
            if (string.IsNullOrWhiteSpace(OldValue))
                errors.Add("OldValue is required for replace mode");
        }
        else if (Mode == StringMode.Trim || Mode == StringMode.Upper || Mode == StringMode.Lower)
        {
            if (string.IsNullOrWhiteSpace(Column))
                errors.Add($"Column is required for {Mode.ToString().ToLower()} mode");
        }

        return errors.ToArray();
    }
}

public enum StringMode
{
    Substring,
    Concat,
    Replace,
    Trim,
    Upper,
    Lower
}

public enum TrimMode
{
    Both,
    Start,
    End
}
