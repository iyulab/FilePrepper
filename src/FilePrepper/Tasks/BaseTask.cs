using CsvHelper;
using FilePrepper.Tasks.Merge;
using FilePrepper.Utils;
using Microsoft.Extensions.Logging;

namespace FilePrepper.Tasks;

public abstract class BaseTask<TOption> : ITask
    where TOption : class, ITaskOption
{
    protected readonly ILogger _logger;
    protected List<string> _originalHeaders = [];
    private int _inputColumnCount;
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

                // Thrown, the message reaches the caller, who reports it; logging it as well said it twice.
                if (!Options.IgnoreErrors)
                {
                    throw new ValidationException(errorMessage, ValidationExceptionErrorCode.General);
                }

                _logger.LogError(errorMessage);
                return false;
            }

            // 실행
            var records = await ReadAndPreProcessAsync(context);
            records = records.Count == 0 && Options.HasHeader && _originalHeaders.Count > 0
                ? await ProcessAnInputWithoutRowsAsync()
                : await ProcessRecordsAsync(records);
            await PostProcessAndWriteAsync(records, context);
            return true;
        }
        catch (Exception ex) when (Options.IgnoreErrors)
        {
            _logger.LogWarning(ex, "Error ignored in {TaskName} task: {Message}", Name, ex.Message);
            return false;
        }
        // Any other exception reaches the caller as it is, with its own stack: the task used to log it here (with the stack)
        // and rethrow it with `throw ex`, which restarted the stack, and the caller logged it again.
    }

    protected virtual bool ValidateTask(TaskContext context, out string[] errors)
    {
        var errorList = new List<string>();

        // 옵션 검증만 수행
        errorList.AddRange(context.Options.Validate());

        // Task별 추가 검증
        errorList.AddRange(ValidateTaskSpecific(context));

        if (errorList.Count == 0)
        {
            errorList.AddRange(MissingRequiredColumns(context));
        }

        errors = [.. errorList];
        return !errors.Any();
    }

    protected virtual string[] ValidateTaskSpecific(TaskContext context)
    {
        return Array.Empty<string>();
    }

    protected List<string> GetFileHeaders(string inputPath)
    {
        if (DataFileFormats.FromPath(inputPath) != DataFileFormat.Csv)
        {
            // Only CSV has a header line to read on its own; every other format is read whole once,
            // here, and the read is kept for ReadAndPreProcessAsync.
            return [.. ReadInputAsync(inputPath).GetAwaiter().GetResult().headers];
        }

        using var reader = CsvUtils.CreateReader(inputPath, Options.Encoding);
        using var csv = new CsvReader(reader, CsvUtils.GetDefaultConfiguration());

        for (int i = 0; i < Options.SkipRows; i++)
            csv.Parser.Read();

        csv.Read();
        csv.ReadHeader();
        return csv.HeaderRecord?.ToList() ?? new List<string>();
    }

    public bool Execute(TaskContext context)
    {
        return ExecuteAsync(context).GetAwaiter().GetResult();
    }

    private async Task<List<Dictionary<string, string>>> ReadAndPreProcessAsync(TaskContext context)
    {
        _logger.LogInformation("Reading input file: {InputPath}", context.InputPath);

        var (records, headers) = await ReadInputAsync(context.InputPath);

        _originalHeaders = headers;
        _inputColumnCount = headers.Count;
        return await PreProcessRecordsAsync(records);
    }

    private readonly Dictionary<string, Task<(List<Dictionary<string, string>> records, List<string> headers)>> _reads =
        new(StringComparer.Ordinal);

    /// <summary>
    /// A file read by its format — the one place a task decides how to read an input, so validation
    /// and processing see the same columns. Each path is read at most once per task.
    /// </summary>
    protected Task<(List<Dictionary<string, string>> records, List<string> headers)> ReadInputAsync(string path)
    {
        if (_reads.TryGetValue(path, out var cached))
            return cached;

        var read = DataFileFormats.FromPath(path) switch
        {
            DataFileFormat.Excel => ReadExcelFileAsync(path),
            DataFileFormat.Parquet => ParquetUtils.ReadParquetFileAsync(path),
            DataFileFormat.Json => JsonUtils.ReadJsonFileAsync(path, Options.JsonRecordPath),
            DataFileFormat.SvmLight => SvmLightUtils.ReadSvmLightFileAsync(path),
            _ => ReadCsvFileAsync(path)
        };
        var checkedRead = RefuseUnreadableColumnsAsync(path, read);
        _reads[path] = checkedRead;
        return checkedRead;
    }

    private static async Task<(List<Dictionary<string, string>> records, List<string> headers)> RefuseUnreadableColumnsAsync(
        string path, Task<(List<Dictionary<string, string>> records, List<string> headers)> read)
    {
        var result = await read;
        HeaderNames.RefuseNone(result.headers, path);
        HeaderNames.RefuseRepeated(result.headers, path);
        return result;
    }

    /// <summary>
    /// The columns of the output as the task makes them when the input has no rows. The output header is read off the first
    /// record, so a table with no rows used to come out with the <i>input's</i> header whatever the task did to the columns
    /// (<c>add-columns</c> without its new column, <c>aggregate</c> without its group and total, <c>unpivot</c> as wide as it came).
    /// </summary>
    private IReadOnlyList<string>? _headersWithoutRows;

    /// <summary>
    /// True when the task decides its output columns from the values in the rows (the categories of a column, the length of a
    /// list): with no rows there is nothing to decide from, and the task refuses instead of guessing a schema.
    /// </summary>
    protected virtual bool ColumnsDependOnTheRows => false;

    /// <summary>
    /// The columns of the output when the options alone decide them and a task can say so without seeing a row (it is also what
    /// an output that came out with no rows is headed with). The default is none: the task is then run on a probe row, see
    /// <see cref="ProcessAnInputWithoutRowsAsync"/>. Where a task declares its columns it keeps a test that the declaration and
    /// its rows agree.
    /// </summary>
    protected virtual IReadOnlyList<string>? DeclaredOutputHeaders() => null;

    /// <summary>
    /// What a probe row holds in every cell, in the order tried: a task that reads a number or a date out of a cell makes its new
    /// columns only for a cell it can read, so one fill alone gives a schema with the numeric columns missing, or the date ones.
    /// </summary>
    private static readonly string[] ProbeFills = ["", "0", "2001-01-01 00:00:00"];

    /// <summary>
    /// Runs the task on a single row of each fill and takes the widest header it made, then drops the rows: the task's own code
    /// decides the schema (a per-task description of it would be a second place to drift). A task that makes no row of a probe
    /// (it filters it out, or needs two rows) or cannot take one is run on the empty input as before, and the output is headed as
    /// the input was unless the task declares its columns.
    /// </summary>
    private async Task<List<Dictionary<string, string>>> ProcessAnInputWithoutRowsAsync()
    {
        if (ColumnsDependOnTheRows)
        {
            throw new InvalidDataException(
                $"The {Name} task decides its columns from the values in the rows, and the input has no rows.");
        }

        var headersRead = _originalHeaders.ToList();
        var declared = DeclaredOutputHeaders();
        if (declared is not null)
        {
            _headersWithoutRows = declared;
            return await ProcessRecordsAsync([]);
        }

        string? bestFill = null;
        List<string>? best = null;
        foreach (var fill in ProbeFills)
        {
            _originalHeaders = headersRead.ToList();
            try
            {
                var made = await ProcessRecordsAsync([ProbeRow(headersRead, fill)]);
                var columns = made.FirstOrDefault()?.Keys.ToList();
                if (columns is not null && (best is null || columns.Count > best.Count))
                {
                    best = columns;
                    bestFill = fill;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "The {TaskName} task cannot take a probe row of '{Fill}'", Name, fill);
            }
        }

        _originalHeaders = headersRead.ToList();
        if (best is null)
        {
            _headersWithoutRows = null;
            return await ProcessRecordsAsync([]);
        }

        // The probe that made the widest header runs once more, so the state the task keeps is that probe's and no other's.
        await ProcessRecordsAsync([ProbeRow(headersRead, bestFill!)]);
        _headersWithoutRows = best;
        return [];
    }

    private static Dictionary<string, string> ProbeRow(IEnumerable<string> headers, string fill) =>
        headers.Distinct(StringComparer.Ordinal).ToDictionary(header => header, _ => fill, StringComparer.Ordinal);

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

    /// <summary>
    /// The columns the task acts on, which the input must have: a name that is not a column of the input is a validation
    /// error before anything is read or written. The default is the target columns of a <see cref="BaseColumnOption"/>;
    /// a task whose columns are named elsewhere overrides it, and a task that tolerates absent columns (or creates its
    /// own) returns none and says what it did not find with <see cref="WarnOfNamesNotInTheInput"/>.
    /// </summary>
    /// <remarks>
    /// Nothing called this: nine tasks overrode it and one commented that "column existence will be validated by
    /// GetRequiredColumns", while thirteen commands succeeded on a column that was not there, having done nothing to it.
    /// </remarks>
    protected virtual IEnumerable<string> GetRequiredColumns() =>
        Options is BaseColumnOption columnOption ? columnOption.TargetColumns : Array.Empty<string>();

    private string[] MissingRequiredColumns(TaskContext context)
    {
        var required = GetRequiredColumns()
            .Where(column => !string.IsNullOrWhiteSpace(column))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (required.Count == 0 || string.IsNullOrEmpty(context.InputPath))
            return [];

        // The read is kept (one per path), so the task's own read afterwards is this one.
        var headers = ReadInputAsync(context.InputPath).GetAwaiter().GetResult().headers;
        var missing = required.Where(column => !headers.Contains(column, StringComparer.Ordinal)).ToList();
        if (missing.Count == 0)
            return [];

        var shown = string.Join(", ", headers.Take(20)) + (headers.Count > 20 ? $", ... ({headers.Count} in all)" : string.Empty);
        return [.. missing.Select(column => $"Column '{column}' not found in the input file (columns: {shown})")];
    }

    /// <summary>
    /// For a task that tolerates names that are not columns of the input: log the ones that matched nothing, so a mistyped
    /// name is not a silent no-op. Call it from <c>ProcessRecordsAsync</c>, before the task changes <c>_originalHeaders</c>.
    /// </summary>
    protected void WarnOfNamesNotInTheInput(IEnumerable<string> names, string consequence)
    {
        var absent = names.Where(name => !_originalHeaders.Contains(name, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal).ToList();
        if (absent.Count > 0)
        {
            _logger.LogWarning("Not a column of the input: {Names} ({Consequence}). Columns: {Columns}",
                string.Join(", ", absent), consequence, string.Join(", ", _originalHeaders.Take(20)));
        }
    }

    protected virtual async Task<(List<Dictionary<string, string>> records, List<string> headers)> ReadExcelFileAsync(string path)
    {
        _logger.LogInformation("Reading Excel file: {Path}", path);
        var (records, headers) = await ExcelUtils.ReadExcelFileAsync(path, Options.HasHeader);
        _logger.LogInformation("Finished reading Excel file {Path}. Read {Count} records", path, records.Count);
        return (records, headers);
    }

    protected virtual async Task<(List<Dictionary<string, string>> records, List<string> headers)> ReadCsvFileAsync(string path)
    {
        using var reader = CsvUtils.CreateReader(path, Options.Encoding);
        using var parser = new CsvParser(reader, CsvUtils.GetDefaultConfiguration(Options.HasHeader, DataFileFormats.DelimiterFor(path)));

        // Skip rows before header
        for (int i = 0; i < Options.SkipRows; i++)
            await parser.ReadAsync();

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

    /// <summary>
    /// A table that had columns and now has none has nothing to write: the writer would fall back to the
    /// input's headers over blank cells (rows present) or to a placeholder (no rows), and the task would report
    /// success. An input with no columns at all is refused where it is read (<see cref="HeaderNames.RefuseNone"/>).
    /// </summary>
    private void RefuseAnEmptiedTable(List<Dictionary<string, string>> records, IEnumerable<string> headers)
    {
        var columnsLeft = records.Count > 0 || _headersWithoutRows is not null ? headers.Count() : _originalHeaders.Count;
        if (columnsLeft == 0)
        {
            throw new InvalidOperationException(
                $"The {Name} task left no columns, so there is nothing to write ({_inputColumnCount} column(s) and {records.Count} row(s) were read).");
        }
    }

    private async Task PostProcessAndWriteAsync(
        List<Dictionary<string, string>> records,
        TaskContext context)
    {
        records = await PostProcessRecordsAsync(records);
        _logger.LogInformation("Writing output file: {OutputPath}", context.OutputPath);

        if (records.Count == 0)
            _headersWithoutRows ??= DeclaredOutputHeaders();

        var headers = records.FirstOrDefault()?.Keys ?? _headersWithoutRows ?? Enumerable.Empty<string>();
        RefuseAnEmptiedTable(records, headers);
        await WriteOutputAsync(context.OutputPath!, headers, records);

        _logger.LogInformation("Task completed successfully");
    }
}