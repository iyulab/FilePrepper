using FilePrepper.Utils;
﻿namespace FilePrepper.Tasks.ColumnInteraction;

public enum OperationType
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Concat,
    Custom
}

public class ColumnInteractionOption : SingleInputOption, IDefaultValueOption
{
    public string[] SourceColumns { get; set; } = Array.Empty<string>();
    public OperationType Operation { get; set; }
    public string OutputColumn { get; set; } = string.Empty;
    public string? CustomExpression { get; set; }
    public string? DefaultValue { get; set; }

    protected override string[] ValidateInternal()
    {
        var errors = new List<string>();

        if (SourceColumns == null || SourceColumns.Length < 2)
        {
            errors.Add("At least two source columns must be specified");
        }
        else
        {
            foreach (var column in SourceColumns)
            {
                if (string.IsNullOrWhiteSpace(column))
                {
                    errors.Add("Source column name cannot be empty or whitespace");
                }
            }
        }

        if (string.IsNullOrWhiteSpace(OutputColumn))
        {
            errors.Add("Output column name cannot be empty or whitespace");
        }

        if (Operation == OperationType.Custom && string.IsNullOrWhiteSpace(CustomExpression))
        {
            errors.Add("Custom expression cannot be empty when using Custom operation type");
        }

        if (!string.IsNullOrWhiteSpace(DefaultValue))
        {
            if (Operation != OperationType.Concat && !NumberText.TryParse(DefaultValue, out _))
            {
                errors.Add("Default value must be a valid number for numeric operations");
            }
        }

        return [.. errors];
    }
}