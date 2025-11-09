using FilePrepper.Tasks;

namespace FilePrepper.Tasks.MergeAsOf;

public class MergeAsOfOption : MultipleInputOption
{
    /// <summary>
    /// Column to use for matching in the left (primary) file
    /// </summary>
    public string LeftOnColumn { get; set; } = string.Empty;

    /// <summary>
    /// Column to use for matching in the right (secondary) file
    /// </summary>
    public string RightOnColumn { get; set; } = string.Empty;

    /// <summary>
    /// Direction for matching: backward (default), forward, or nearest
    /// </summary>
    public AsOfDirection Direction { get; set; } = AsOfDirection.Backward;

    /// <summary>
    /// Maximum time difference allowed for matching (in seconds)
    /// If null, no tolerance limit is applied
    /// </summary>
    public double? Tolerance { get; set; }

    /// <summary>
    /// Suffix to add to right file columns to avoid name conflicts
    /// </summary>
    public string Suffix { get; set; } = "_right";

    /// <summary>
    /// Allow multiple matches per left record
    /// If false (default), only the best match is used
    /// </summary>
    public bool AllowMultipleMatches { get; set; } = false;

    protected override string[] ValidateInternal()
    {
        var errors = new List<string>();

        // 1) Basic input validation
        if (!ValidateBasicInputs(errors))
            return errors.ToArray();

        // 2) Exactly 2 files required for merge_asof
        if (InputPaths.Count != 2)
        {
            errors.Add($"merge_asof requires exactly 2 files, but {InputPaths.Count} files were provided.");
        }

        // 3) Column validation
        if (string.IsNullOrWhiteSpace(LeftOnColumn))
        {
            errors.Add("LeftOnColumn (left file matching column) is required.");
        }

        if (string.IsNullOrWhiteSpace(RightOnColumn))
        {
            errors.Add("RightOnColumn (right file matching column) is required.");
        }

        // 4) Tolerance validation
        if (Tolerance.HasValue && Tolerance.Value < 0)
        {
            errors.Add("Tolerance must be a non-negative value (in seconds).");
        }

        // 5) Header validation - required for column name matching
        if (!HasHeader)
        {
            errors.Add("Header is required for merge_asof operation (--header flag must be set).");
        }

        return errors.ToArray();
    }

    private bool ValidateBasicInputs(List<string> errors)
    {
        if (InputPaths == null || InputPaths.Count < 2)
        {
            errors.Add("At least two input files must be specified for merge_asof.");
            return false;
        }

        foreach (var (path, index) in InputPaths.Select((p, i) => (p, i)))
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                errors.Add($"Input path at index {index} cannot be empty or whitespace.");
            }
            else if (!File.Exists(path))
            {
                errors.Add($"Input file does not exist: {path}");
            }
        }

        return !errors.Any();
    }
}

public enum AsOfDirection
{
    /// <summary>
    /// Match with the most recent value in right file that is <= left value (default)
    /// </summary>
    Backward,

    /// <summary>
    /// Match with the nearest future value in right file that is >= left value
    /// </summary>
    Forward,

    /// <summary>
    /// Match with the closest value in right file, regardless of direction
    /// </summary>
    Nearest
}
