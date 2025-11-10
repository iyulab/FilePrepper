namespace FilePrepper.Pipeline;

/// <summary>
/// Types of join operations for combining two DataPipelines
/// </summary>
public enum JoinType
{
    /// <summary>
    /// Inner join: only rows with matching keys in both datasets
    /// </summary>
    Inner,

    /// <summary>
    /// Left join: all rows from left dataset, matching rows from right (null for non-matches)
    /// </summary>
    Left,

    /// <summary>
    /// Right join: all rows from right dataset, matching rows from left (null for non-matches)
    /// </summary>
    Right,

    /// <summary>
    /// Outer join: all rows from both datasets (null for non-matches on either side)
    /// </summary>
    Outer
}
