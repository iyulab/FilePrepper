using CsvHelper;
using FilePrepper.Tasks.Merge;
using Microsoft.Extensions.Logging;

namespace FilePrepper.Tasks;

public abstract class BaseTask<TOption> : ITask 
    where TOption : class, ITaskOption
{
    protected readonly ILogger _logger;
    protected List<string> _originalHeaders = [];
    private TaskContext _context = null!;

    protected TOption Options
    {
        get
        {
            if (_context == null)
            {
                throw new InvalidOperationException("Task context has not been initialized");
            }
            return _context.GetOptions<TOption>();
        }
    }

    ITaskOption ITask.Options => Options;

    public string Name => GetType().Name.Replace("Task", string.Empty);

    protected BaseTask(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> ExecuteAsync(TaskContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;

        try
        {
            // 검증 수행
            if (!ValidateTask(context, out var validationErrors))
            {
                var errorMessage = $"Validation errors in {Name} task: {string.Join(", ", validationErrors)}";
                _logger.LogError(errorMessage);

                if (!Options.IgnoreErrors)
                {
                    throw new ValidationException(errorMessage, ValidationExceptionErrorCode.General);
                }
                return false;
            }

            // 실행
            var records = await ReadAndPreProcessAsync(context);
            records = await ProcessRecordsAsync(records);
            await PostProcessAndWriteAsync(records, context);
            return true;
        }
        catch (Exception ex)
        {
            HandleTaskException(ex);
            return false;
        }
    }

    protected virtual bool ValidateTask(TaskContext context, out string[] errors)
    {
        var errorList = new List<string>();

        // 옵션 검증만 수행
        errorList.AddRange(context.Options.Validate());

        // Task별 추가 검증
        errorList.AddRange(ValidateTaskSpecific(context));

        errors = [.. errorList];
        return !errors.Any();
    }

    protected virtual string[] ValidateTaskSpecific(TaskContext context)
    {
        return Array.Empty<string>();
    }

    protected List<string> GetFileHeaders(string inputPath)
    {
        using var reader = new StreamReader(inputPath);
        using var csv = new CsvReader(reader, CsvUtils.GetDefaultConfiguration());

        csv.Read();
        csv.ReadHeader();
        return csv.HeaderRecord?.ToList() ?? new List<string>();
    }

    private void HandleTaskException(Exception ex)
    {
        if (Options.IgnoreErrors)
        {
            _logger.LogWarning(ex, "Error ignored in {TaskName} task: {Message}", Name, ex.Message);
            return;
        }

        _logger.LogError(ex, "Error executing {TaskName} task: {Message}", Name, ex.Message);
        throw ex;
    }

    public bool Execute(TaskContext context)
    {
        return ExecuteAsync(context).GetAwaiter().GetResult();
    }

    private async Task<List<Dictionary<string, string>>> ReadAndPreProcessAsync(TaskContext context)
    {
        _logger.LogInformation("Reading input file: {InputPath}", context.InputPath);

        // Check if file is Excel format
        var (records, headers) = ExcelUtils.IsExcelFile(context.InputPath)
            ? await ReadExcelFileAsync(context.InputPath)
            : await ReadCsvFileAsync(context.InputPath);

        _originalHeaders = headers;
        return await PreProcessRecordsAsync(records);
    }

    protected abstract Task<List<Dictionary<string, string>>> ProcessRecordsAsync(
        List<Dictionary<string, string>> records);

    protected virtual Task<List<Dictionary<string, string>>> PreProcessRecordsAsync(
        List<Dictionary<string, string>> records)
    {
        return Task.FromResult(records);
    }

    protected virtual Task<List<Dictionary<string, string>>> PostProcessRecordsAsync(
        List<Dictionary<string, string>> records)
    {
        return Task.FromResult(records);
    }

    protected virtual IEnumerable<string> GetRequiredColumns() =>
        Options is BaseColumnOption columnOption ? columnOption.TargetColumns : Array.Empty<string>();

    protected virtual async Task<(List<Dictionary<string, string>> records, List<string> headers)> ReadExcelFileAsync(string path)
    {
        _logger.LogInformation("Reading Excel file: {Path}", path);
        var (records, headers) = await ExcelUtils.ReadExcelFileAsync(path, Options.HasHeader);
        _logger.LogInformation("Finished reading Excel file {Path}. Read {Count} records", path, records.Count);
        return (records, headers);
    }

    protected virtual async Task<(List<Dictionary<string, string>> records, List<string> headers)> ReadCsvFileAsync(string path)
    {
        _logger.LogInformation("Reading input file: {Path}", path);

        using var reader = new StreamReader(path);
        using var parser = new CsvParser(reader, CsvUtils.GetDefaultConfiguration(Options.HasHeader));

        var records = new List<Dictionary<string, string>>();
        var headers = new List<string>();

        // Read first row
        if (await parser.ReadAsync())
        {
            var firstRow = parser.Record!;
            var fieldCount = firstRow.Length;

            if (Options.HasHeader)
            {
                // Use actual headers from the file
                headers.AddRange(firstRow!);
            }
            else
            {
                // Use numeric indices as headers
                headers.AddRange(Enumerable.Range(0, fieldCount).Select(i => i.ToString()));

                // Add first row as data when HasHeader is false
                var record = new Dictionary<string, string>();
                for (int i = 0; i < fieldCount; i++)
                {
                    record[headers[i]] = firstRow[i];
                }
                records.Add(record);
            }

            _logger.LogDebug("Using headers: {Headers}", string.Join(", ", headers));
        }

        // Read remaining records
        while (await parser.ReadAsync())
        {
            var record = new Dictionary<string, string>();
            for (int i = 0; i < parser.Record!.Length && i < headers.Count; i++)
            {
                record[headers[i]] = parser.Record[i]!;
            }
            records.Add(record);
            _logger.LogDebug("Added row: {Row}", string.Join(", ", record.Values));
        }

        _logger.LogInformation("Finished reading file {Path}. Read {Count} records", path, records.Count);
        return (records, headers);
    }

    protected virtual async Task WriteOutputAsync(
    string outputPath,
    IEnumerable<string> headers,
    IEnumerable<Dictionary<string, string>> records)
    {
        var finalHeaders = headers.Any() ? headers : _originalHeaders;
        if (!finalHeaders.Any())
        {
            finalHeaders = ["NoData"];
        }

        await using var writer = new StreamWriter(outputPath);
        await using var csv = new CsvWriter(writer, CsvUtils.GetDefaultConfiguration(Options.HasHeader));

        // HasHeader가 true일 때만 헤더 작성
        if (Options.HasHeader)
        {
            foreach (var header in finalHeaders)
            {
                csv.WriteField(header);
            }
            csv.NextRecord();
        }

        // 데이터 작성
        foreach (var record in records)
        {
            foreach (var header in finalHeaders)
            {
                csv.WriteField(record.GetValueOrDefault(header, string.Empty));
            }
            csv.NextRecord();
        }
    }

    private async Task PostProcessAndWriteAsync(
        List<Dictionary<string, string>> records,
        TaskContext context)
    {
        records = await PostProcessRecordsAsync(records);
        _logger.LogInformation("Writing output file: {OutputPath}", context.OutputPath);

        var headers = records.FirstOrDefault()?.Keys ?? Enumerable.Empty<string>();
        await WriteOutputAsync(context.OutputPath!, headers, records);

        _logger.LogInformation("Task completed successfully");
    }
}