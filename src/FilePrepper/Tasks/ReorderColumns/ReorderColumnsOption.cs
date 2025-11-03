using FilePrepper.Tasks;

namespace FilePrepper.Tasks.ReorderColumns;

public class ReorderColumnsOption : SingleInputOption
{
    /// <summary>
    /// Desired column order.
    /// </summary>
    public List<string> Order { get; set; } = new();

    protected override string[] ValidateInternal()
    {
        var errors = new List<string>();
        if (Order == null || Order.Count == 0)
        {
            errors.Add("At least one column must be specified for reordering.");
            return [.. errors];
        }
        foreach (var col in Order)
        {
            if (string.IsNullOrWhiteSpace(col))
            {
                errors.Add("Column name in order cannot be empty or whitespace.");
            }
        }
        return [.. errors];
    }
}
