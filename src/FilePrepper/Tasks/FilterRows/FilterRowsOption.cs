namespace FilePrepper.Tasks.FilterRows;

public enum FilterOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    GreaterOrEqual,
    LessThan,
    LessOrEqual,
    Contains,
    NotContains,
    StartsWith,
    EndsWith
}

public class FilterCondition
{
    public string ColumnName { get; set; } = string.Empty;
    public FilterOperator Operator { get; set; }
    public string Value { get; set; } = string.Empty;
}

public class FilterRowsOption : SingleInputOption
{
    // 여러 조건을 모두 만족해야 하는 AND 방식으로 처리
    public List<FilterCondition> Conditions { get; set; } = new();

    protected override string[] ValidateInternal()
    {
        var errors = new List<string>();

        if (Conditions.Count == 0)
        {
            errors.Add("Filter conditions must not be empty.");
        }
        else
        {
            foreach (var cond in Conditions)
            {
                if (string.IsNullOrWhiteSpace(cond.ColumnName))
                {
                    errors.Add("ColumnName in filter condition cannot be empty.");
                }
            }
        }

        return [.. errors];
    }
}