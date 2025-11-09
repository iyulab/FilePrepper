namespace FilePrepper.Tasks;

/// <summary>
/// 컬럼을 이름 또는 인덱스로 지정
/// </summary>
public class ColumnIdentifier
{
    public string? Name { get; set; }
    public int? Index { get; set; }

    public bool IsValid => Name != null || Index != null;

    public static ColumnIdentifier ByName(string name) => new() { Name = name };
    public static ColumnIdentifier ByIndex(int index) => new() { Index = index };

    /// <summary>
    /// Parse a string into a ColumnIdentifier. If the string is a valid integer, it's treated as an index; otherwise, it's treated as a name.
    /// </summary>
    public static ColumnIdentifier Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Column identifier cannot be empty or whitespace", nameof(value));
        }

        if (int.TryParse(value, out int index))
        {
            return ByIndex(index);
        }

        return ByName(value);
    }

    public override string ToString()
    {
        if (Name != null)
        {
            return Name;
        }

        if (Index.HasValue)
        {
            return Index.Value.ToString();
        }

        return "<invalid>";
    }
}