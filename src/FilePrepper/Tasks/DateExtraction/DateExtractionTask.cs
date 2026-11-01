namespace FilePrepper.Tasks.DateExtraction;

public class DateExtractionTask : BaseTask<DateExtractionOption>
{
    public DateExtractionTask(ILogger<DateExtractionTask> logger) : base(logger)
    {
    }

    protected override IEnumerable<string> GetRequiredColumns() => Options.Extractions.Select(extraction => extraction.SourceColumn);

    protected override Task<List<Dictionary<string, string>>> ProcessRecordsAsync(
        List<Dictionary<string, string>> records)
    {
        var processedRecords = new List<Dictionary<string, string>>();

        foreach (var record in records)
        {
            var newRecord = new Dictionary<string, string>(record);

            foreach (var extraction in Options.Extractions)
            {
                try
                {
                    ProcessDateExtraction(newRecord, extraction);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to parse date value '{Value}' for column {Column}",
                        record[extraction.SourceColumn], extraction.SourceColumn);

                    // 실패한 컴포넌트들에 대해 빈 값 설정
                    foreach (var component in extraction.Components)
                    {
                        var columnName = GetOutputColumnName(extraction, component);
                        newRecord[columnName] = string.Empty;
                    }

                    if (!Options.IgnoreErrors)
                    {
                        throw new ValidationException(
                            $"Failed to parse date value '{record[extraction.SourceColumn]}' for column {extraction.SourceColumn}: {ex.Message}",
                            ValidationExceptionErrorCode.General);
                    }
                }
            }

            processedRecords.Add(newRecord);
        }

        return Task.FromResult(processedRecords);
    }

    private void ProcessDateExtraction(Dictionary<string, string> record, DateColumnExtraction extraction)
    {
        if (!record.TryGetValue(extraction.SourceColumn, out string? value) ||
            string.IsNullOrWhiteSpace(value))
        {
            foreach (var component in extraction.Components)
            {
                var columnName = GetOutputColumnName(extraction, component);
                record[columnName] = string.Empty;
            }
            return;
        }

        var culture = extraction.Culture ?? CultureInfo.InvariantCulture;
        var read = extraction.DateFormat != null
            ? DateText.TryParseExact(value, extraction.DateFormat, culture, out var dateTime)
            : DateText.TryParse(value, culture, out dateTime);
        if (!read)
        {
            throw new ValidationException(
                extraction.DateFormat != null
                    ? $"Failed to parse date value '{value}' for column {extraction.SourceColumn}: not a date in the format '{extraction.DateFormat}'"
                    : $"Failed to parse date value '{value}' for column {extraction.SourceColumn}: not a date",
                ValidationExceptionErrorCode.General);
        }

        foreach (var component in extraction.Components)
        {
            var componentValue = ExtractComponent(dateTime, component);
            var columnName = GetOutputColumnName(extraction, component);
            record[columnName] = componentValue;
        }
    }

    private string ExtractComponent(DateTime date, DateComponent component) =>
        component switch
        {
            DateComponent.Year => date.Year.ToString(),
            DateComponent.Month => date.Month.ToString(),
            DateComponent.Day => date.Day.ToString(),
            DateComponent.Hour => date.Hour.ToString(),
            DateComponent.Minute => date.Minute.ToString(),
            DateComponent.Second => date.Second.ToString(),
            DateComponent.DayOfWeek => ((int)date.DayOfWeek).ToString(),
            DateComponent.WeekOfYear => ISOWeek.GetWeekOfYear(date).ToString(),
            DateComponent.Quarter => ((date.Month - 1) / 3 + 1).ToString(),
            DateComponent.DayOfYear => date.DayOfYear.ToString(),
            _ => throw new ValidationException($"Unsupported date component: {component}",
                ValidationExceptionErrorCode.General)
        };

    private string GetOutputColumnName(DateColumnExtraction extraction, DateComponent component)
    {
        if (!Options.AppendToSource)
        {
            return extraction.OutputColumnTemplate!
                .Replace("{column}", extraction.SourceColumn)
                .Replace("{component}", component.ToString());
        }

        return Options.OutputColumnTemplate!
            .Replace("{column}", extraction.SourceColumn)
            .Replace("{component}", component.ToString());
    }
}