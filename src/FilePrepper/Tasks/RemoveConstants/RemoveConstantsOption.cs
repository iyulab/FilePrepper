namespace FilePrepper.Tasks.RemoveConstants;

public class RemoveConstantsOption : SingleInputOption
{
    /// <summary>
    /// Unique ratio (distinct values ÷ rows) at or below which a column is removed.
    /// 0.0 = remove only exact constants (one distinct value, or none but empty cells).
    /// 0.01 = also remove columns whose distinct values are at most 1 % of the rows — which includes a
    /// category of a few levels over many rows, not only a column dominated by one value.
    /// Values are compared exactly as text.
    /// </summary>
    public double UniqueRatioThreshold { get; set; } = 0.0;

    /// <summary>
    /// When true, only reports constant columns without removing them.
    /// </summary>
    public bool ReportOnly { get; set; } = false;

    protected override string[] ValidateInternal()
    {
        var errors = new List<string>();

        if (double.IsNaN(UniqueRatioThreshold) || UniqueRatioThreshold < 0 || UniqueRatioThreshold > 1)
        {
            errors.Add("UniqueRatioThreshold must be between 0.0 and 1.0");
        }

        return [.. errors];
    }
}
