namespace FilePrepper.Utils;

/// <summary>
/// A JSON document that is not a table as it stands. The message says what was found;
/// <see cref="RecordPaths"/> holds the dotted paths to arrays of objects that would read it as one —
/// empty when the document keeps no rows to point at — so a caller can offer them in its own terms.
/// </summary>
public sealed class JsonShapeException(string message, IReadOnlyList<string> recordPaths) : FormatException(message)
{
    /// <summary>Record paths that would read the document as a table, shallowest first.</summary>
    public IReadOnlyList<string> RecordPaths { get; } = recordPaths;
}
