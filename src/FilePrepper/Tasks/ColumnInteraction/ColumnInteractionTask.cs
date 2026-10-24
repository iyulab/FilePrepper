using FilePrepper.Utils;
﻿using System.Data;

namespace FilePrepper.Tasks.ColumnInteraction;

public class ColumnInteractionTask : BaseTask<ColumnInteractionOption>
{
    public ColumnInteractionTask(ILogger<ColumnInteractionTask> logger) : base(logger)
    {
    }

    protected override async Task<List<Dictionary<string, string>>> ProcessRecordsAsync(
        List<Dictionary<string, string>> records)
    {
        // 헤더 검증
        var headers = records.FirstOrDefault()?.Keys.ToList() ?? new List<string>();
        if (headers.Contains(Options.OutputColumn))
        {
            throw new ValidationException($"Output column already exists: {Options.OutputColumn}",
                ValidationExceptionErrorCode.General);
        }

        // 컬럼 연산 수행
        await Task.Run(() =>
        {
            foreach (var record in records)
            {
                try
                {
                    record[Options.OutputColumn] = ProcessRecord(record);
                }
                catch (ValidationException) when (Options.IgnoreErrors)
                {
                    _logger.LogWarning("Error processing row");
                    record[Options.OutputColumn] = Options.DefaultValue ?? string.Empty;
                }
                catch (Exception ex) when (Options.IgnoreErrors)
                {
                    _logger.LogWarning("Error processing row: {Error}", ex.Message);
                    record[Options.OutputColumn] = Options.DefaultValue ?? string.Empty;
                }
            }
        });

        return records;
    }

    private string ProcessRecord(Dictionary<string, string> record)
    {
        var values = Options.SourceColumns
            .Select(col => record.GetValueOrDefault(col, string.Empty))
            .ToArray();

        return Options.Operation switch
        {
            OperationType.Concat => string.Join(string.Empty, values),
            OperationType.Custom => ProcessCustomOperation(values),
            _ => ProcessNumericOperation(values)
        };
    }

    private string ProcessNumericOperation(string[] values)
    {
        var numbers = GetNumericValues(values);
        if (numbers.Count == 0) return string.Empty;

        try
        {
            double result = Options.Operation switch
            {
                OperationType.Add => numbers.Sum(),
                OperationType.Subtract => numbers.Aggregate((a, b) => a - b),
                OperationType.Multiply => numbers.Aggregate((a, b) => a * b),
                OperationType.Divide => numbers.Aggregate((a, b) =>
                    b != 0 ? a / b : throw new ValidationException("Division by zero", ValidationExceptionErrorCode.General)),
                _ => throw new ValidationException($"Unsupported operation type: {Options.Operation}", ValidationExceptionErrorCode.General)
            };

            return result.ToString(CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is not ValidationException)
        {
            throw new ValidationException($"Error in numeric operation: {ex.Message}", ValidationExceptionErrorCode.General);
        }
    }

    private string ProcessCustomOperation(string[] values)
    {
        if (string.IsNullOrWhiteSpace(Options.CustomExpression))
        {
            throw new ValidationException("Custom expression is required for Custom operation type",
                ValidationExceptionErrorCode.General);
        }

        string expression = Options.CustomExpression;
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = GetValidNumericValue(values[i]);
            expression = expression.Replace($"${i + 1}", values[i]);
        }

        try
        {
            var dt = new DataTable();
            var result = dt.Compute(expression, "");
            return Convert.ToString(result, CultureInfo.InvariantCulture) ?? string.Empty;
        }
        catch (Exception ex)
        {
            throw new ValidationException($"Error evaluating custom expression: {ex.Message}",
                ValidationExceptionErrorCode.General);
        }
    }

    private List<double> GetNumericValues(string[] values)
    {
        var numbers = new List<double>();

        foreach (var value in values)
        {
            if (CsvUtils.TryParseNumeric(value, out var number))
            {
                numbers.Add(number);
            }
            else if (Options.IgnoreErrors && !string.IsNullOrWhiteSpace(Options.DefaultValue))
            {
                if (NumberText.TryParse(Options.DefaultValue, out var defaultNum))
                {
                    numbers.Add(defaultNum);
                }
                else
                {
                    throw new ValidationException($"Invalid default value: {Options.DefaultValue}",
                        ValidationExceptionErrorCode.General);
                }
            }
            else
            {
                throw new ValidationException($"Invalid numeric value: {value}",
                    ValidationExceptionErrorCode.General);
            }
        }

        return numbers;
    }

    private string GetValidNumericValue(string value)
    {
        if (CsvUtils.TryParseNumeric(value, out _))
        {
            return value;
        }

        if (Options.IgnoreErrors && !string.IsNullOrWhiteSpace(Options.DefaultValue))
        {
            if (NumberText.TryParse(Options.DefaultValue, out _))
            {
                return Options.DefaultValue;
            }
            throw new ValidationException($"Invalid default value: {Options.DefaultValue}",
                ValidationExceptionErrorCode.General);
        }

        throw new ValidationException($"Invalid numeric value: {value}",
            ValidationExceptionErrorCode.General);
    }

    protected override IEnumerable<string> GetRequiredColumns()
    {
        return Options.SourceColumns;
    }
}