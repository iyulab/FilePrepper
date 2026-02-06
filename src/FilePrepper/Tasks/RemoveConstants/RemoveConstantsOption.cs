namespace FilePrepper.Tasks.RemoveConstants;

public class RemoveConstantsOption : SingleInputOption
{
    /// <summary>
    /// Unique ratio threshold for column removal.
    /// 0.0 = remove only exact constants (1 unique value).
    /// 0.01 = also remove columns with unique ratio below 1%.
    /// </summary>
    public double UniqueRatioThreshold { get; set; } = 0.0;

    /// <summary>
    /// When true, only reports constant columns without removing them.
    /// </summary>
    public bool ReportOnly { get; set; } = false;

    protected override string[] ValidateInternal()
    {
        var errors = new List<string>();

        if (UniqueRatioThreshold < 0 || UniqueRatioThreshold > 1)
        {
            errors.Add("UniqueRatioThreshold must be between 0.0 and 1.0");
        }

        return [.. errors];
    }
}
