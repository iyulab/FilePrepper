using Microsoft.Extensions.Logging;

namespace FilePrepper.Tasks.StringOps;

public class StringTask : BaseTask<StringOption>
{
    public StringTask(ILogger<StringTask> logger) : base(logger) { }

    protected override async Task<List<Dictionary<string, string>>> ProcessRecordsAsync(
        List<Dictionary<string, string>> records)
    {
        _logger.LogInformation("Starting String operation: {Mode}", Options.Mode);

        var result = Options.Mode switch
        {
            StringMode.Substring => ApplySubstring(records),
            StringMode.Concat => ApplyConcat(records),
            StringMode.Replace => ApplyReplace(records),
            StringMode.Trim => ApplyTrim(records),
            StringMode.Upper => ApplyUpper(records),
            StringMode.Lower => ApplyLower(records),
            _ => throw new ArgumentException($"Unknown string mode: {Options.Mode}")
        };

        _logger.LogInformation("✓ String operation complete. Processed {Count} records", result.Count);
        return await Task.FromResult(result);
    }

    private List<Dictionary<string, string>> ApplySubstring(List<Dictionary<string, string>> records)
    {
        var outputCol = Options.OutputColumn ?? $"{Options.Column}_substring";
        var startIdx = Options.StartIndex;
        var length = Options.Length;

        _logger.LogInformation("Extracting substring from column: {Column}, start: {Start}, length: {Length}",
            Options.Column, startIdx, length?.ToString() ?? "end");

        foreach (var record in records)
        {
            if (record.TryGetValue(Options.Column, out var value) && !string.IsNullOrEmpty(value))
            {
                try
                {
                    if (startIdx < value.Length)
                    {
                        record[outputCol] = length.HasValue
                            ? value.Substring(startIdx, Math.Min(length.Value, value.Length - startIdx))
                            : value.Substring(startIdx);
                    }
                    else
                    {
                        record[outputCol] = "";
                    }
                }
                catch
                {
                    record[outputCol] = "";
                }
            }
            else
            {
                record[outputCol] = "";
            }
        }

        return records;
    }

    private List<Dictionary<string, string>> ApplyConcat(List<Dictionary<string, string>> records)
    {
        var columns = Options.Columns!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var separator = Options.Separator;
        var outputCol = Options.OutputColumn!;

        _logger.LogInformation("Concatenating {Count} columns with separator: '{Separator}'",
            columns.Length, separator);

        foreach (var record in records)
        {
            var values = columns
                .Select(col => record.TryGetValue(col, out var val) ? val : "")
                .ToArray();

            record[outputCol] = string.Join(separator, values);
        }

        return records;
    }

    private List<Dictionary<string, string>> ApplyReplace(List<Dictionary<string, string>> records)
    {
        var outputCol = Options.OutputColumn ?? Options.Column;
        var oldValue = Options.OldValue!;
        var newValue = Options.NewValue ?? "";

        _logger.LogInformation("Replacing '{Old}' with '{New}' in column: {Column}",
            oldValue, newValue, Options.Column);

        foreach (var record in records)
        {
            if (record.TryGetValue(Options.Column, out var value))
            {
                record[outputCol] = value.Replace(oldValue, newValue);
            }
        }

        return records;
    }

    private List<Dictionary<string, string>> ApplyTrim(List<Dictionary<string, string>> records)
    {
        var outputCol = Options.OutputColumn ?? Options.Column;

        _logger.LogInformation("Trimming column: {Column}, mode: {TrimMode}",
            Options.Column, Options.TrimMode);

        foreach (var record in records)
        {
            if (record.TryGetValue(Options.Column, out var value))
            {
                record[outputCol] = Options.TrimMode switch
                {
                    TrimMode.Both => value.Trim(),
                    TrimMode.Start => value.TrimStart(),
                    TrimMode.End => value.TrimEnd(),
                    _ => value
                };
            }
        }

        return records;
    }

    private List<Dictionary<string, string>> ApplyUpper(List<Dictionary<string, string>> records)
    {
        var outputCol = Options.OutputColumn ?? Options.Column;

        _logger.LogInformation("Converting to uppercase: {Column}", Options.Column);

        foreach (var record in records)
        {
            if (record.TryGetValue(Options.Column, out var value))
            {
                record[outputCol] = value.ToUpper();
            }
        }

        return records;
    }

    private List<Dictionary<string, string>> ApplyLower(List<Dictionary<string, string>> records)
    {
        var outputCol = Options.OutputColumn ?? Options.Column;

        _logger.LogInformation("Converting to lowercase: {Column}", Options.Column);

        foreach (var record in records)
        {
            if (record.TryGetValue(Options.Column, out var value))
            {
                record[outputCol] = value.ToLower();
            }
        }

        return records;
    }
}
