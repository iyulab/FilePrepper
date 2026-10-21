namespace FilePrepper.Tasks.Merge;

public enum MergeType
{
    /// <summary>
    /// 세로로 머지 (union). 모든 컬럼 통합, 중복 열은 그대로 유지
    /// </summary>
    Vertical,

    /// <summary>
    /// 가로로 머지 (join). 지정된 키 컬럼을 기준으로 데이터 결합
    /// </summary>
    Horizontal
}

public enum JoinType
{
    /// <summary>
    /// 두 집합에서 키가 모두 있는 레코드만 결합 (INNER JOIN)
    /// </summary>
    Inner,

    /// <summary>
    /// 왼쪽 집합 기준, 오른쪽이 없어도 결합 (LEFT JOIN)
    /// </summary>
    Left,

    /// <summary>
    /// 오른쪽 집합 기준, 왼쪽이 없어도 결합 (RIGHT JOIN)
    /// </summary>
    Right,

    /// <summary>
    /// 키가 있든 없든 전부 결합 (FULL OUTER JOIN)
    /// </summary>
    Full
}

public class MergeOption : MultipleInputOption
{
    public MergeType MergeType { get; set; } = MergeType.Vertical;
    public JoinType JoinType { get; set; } = JoinType.Inner;

    /// <summary>
    /// Legacy: Join key columns (same name in all files)
    /// </summary>
    public List<ColumnIdentifier> JoinKeyColumns { get; set; } = new();

    /// <summary>
    /// New: Join mappings allowing different column names between files
    /// If specified, takes precedence over JoinKeyColumns
    /// </summary>
    public List<JoinMapping> JoinMappings { get; set; } = new();

    public bool StrictColumnCount { get; set; }

    /// <summary>Whether a join names its columns, which needs a header row in every file.</summary>
    internal bool JoinsByName =>
        MergeType == MergeType.Horizontal &&
        (JoinKeyColumns.Any(k => k.Name != null) ||
         JoinMappings.Any(m => m.LeftColumn.Name != null || m.RightColumn.Name != null));

    /// <summary>
    /// The option's own values. What the files hold — their columns and rows — is checked by
    /// <see cref="MergeTask"/>, which reads each file by its format.
    /// </summary>
    protected override string[] ValidateInternal()
    {
        var errors = new List<string>();

        if (!ValidateBasicInputs(errors))
            return errors.ToArray();

        ValidateJoinKeys(errors);

        if (JoinsByName && !HasHeader)
            errors.Add("Header is required when joining by column names.");

        return errors.ToArray();
    }

    private bool ValidateBasicInputs(List<string> errors)
    {
        if (InputPaths == null || InputPaths.Count < 2)
        {
            errors.Add("At least two input files must be specified for merging.");
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

    private void ValidateJoinKeys(List<string> errors)
    {
        // Check if both JoinKeyColumns and JoinMappings are specified
        if (JoinKeyColumns?.Count > 0 && JoinMappings?.Count > 0)
        {
            errors.Add("Cannot specify both JoinKeyColumns and JoinMappings. Use JoinMappings for heterogeneous column names.");
            return;
        }

        // Validate JoinMappings (new approach)
        if (JoinMappings?.Count > 0)
        {
            if (MergeType != MergeType.Horizontal)
            {
                errors.Add("Join mappings can only be specified for horizontal merge.");
            }
            else
            {
                foreach (var (mapping, index) in JoinMappings.Select((m, i) => (m, i)))
                {
                    if (!mapping.IsValid)
                    {
                        errors.Add($"Join mapping at index {index} is invalid. Both left and right columns must be specified.");
                    }

                    // Validate left column
                    if (mapping.LeftColumn.Index.HasValue && mapping.LeftColumn.Index.Value < 0)
                    {
                        errors.Add($"Left column index at mapping {index} cannot be negative.");
                    }

                    // Validate right column
                    if (mapping.RightColumn.Index.HasValue && mapping.RightColumn.Index.Value < 0)
                    {
                        errors.Add($"Right column index at mapping {index} cannot be negative.");
                    }
                }
            }
        }

        // Validate JoinKeyColumns (legacy approach)
        if (JoinKeyColumns?.Count > 0)
        {
            if (MergeType != MergeType.Horizontal)
            {
                errors.Add("Join key columns can only be specified for horizontal merge.");
            }
            else
            {
                foreach (var (keyCol, index) in JoinKeyColumns.Select((k, i) => (k, i)))
                {
                    if (!keyCol.IsValid)
                    {
                        errors.Add($"Join key column at index {index} must specify either Name or Index.");
                    }
                    if (keyCol.Index.HasValue && keyCol.Index.Value < 0)
                    {
                        errors.Add($"Join key column index at position {index} cannot be negative.");
                    }
                }
            }
        }
    }
}
