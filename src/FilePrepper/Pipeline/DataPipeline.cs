using CsvHelper;
using CsvHelper.Configuration;
using FilePrepper.Tasks.NormalizeData;
using FilePrepper.Utils;
using FilePrepper.Tasks.WindowOps;
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;

namespace FilePrepper.Pipeline;

/// <summary>
/// Fluent API for chaining data transformations without file I/O overhead
/// </summary>
public class DataPipeline
{
    private readonly List<Dictionary<string, string>> _rows;
    private readonly List<string> _columnNames;

    private DataPipeline(IEnumerable<Dictionary<string, string>> rows, IEnumerable<string> columnNames)
    {
        _rows = rows.ToList();
        _columnNames = columnNames.ToList();
    }

    #region Factory Methods

    /// <summary>
    /// Create pipeline from CSV file
    /// </summary>
    public static async Task<DataPipeline> FromCsvAsync(string path, bool hasHeader = true)
    {
        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = hasHeader
        });

        var rows = new List<Dictionary<string, string>>();
        var headers = new List<string>();

        await csv.ReadAsync();
        csv.ReadHeader();
        headers.AddRange(csv.HeaderRecord ?? Array.Empty<string>());

        while (await csv.ReadAsync())
        {
            var row = new Dictionary<string, string>();
            foreach (var header in headers)
            {
                row[header] = csv.GetField(header) ?? string.Empty;
            }
            rows.Add(row);
        }

        return new DataPipeline(rows, headers);
    }

    /// <summary>
    /// Create pipeline from in-memory data
    /// </summary>
    public static DataPipeline FromData(IEnumerable<Dictionary<string, string>> data)
    {
        var dataList = data.ToList();
        if (!dataList.Any())
        {
            return new DataPipeline(Enumerable.Empty<Dictionary<string, string>>(), Enumerable.Empty<string>());
        }

        var columns = dataList.First().Keys.ToList();
        return new DataPipeline(dataList, columns);
    }

    /// <summary>
    /// Create pipeline from Excel file (.xls, .xlsx)
    /// </summary>
    public static async Task<DataPipeline> FromExcelAsync(string path, bool hasHeader = true, string? sheetName = null, int sheetIndex = 0)
    {
        var (rows, headers) = await ExcelUtils.ReadExcelFileAsync(path, hasHeader, sheetName, sheetIndex);
        return new DataPipeline(rows, headers);
    }

    /// <summary>
    /// Create pipeline from JSON file (array of objects)
    /// </summary>
    public static async Task<DataPipeline> FromJsonAsync(string path)
    {
        var jsonContent = await File.ReadAllTextAsync(path);
        var jsonArray = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(jsonContent);

        if (jsonArray == null || !jsonArray.Any())
        {
            return new DataPipeline(Enumerable.Empty<Dictionary<string, string>>(), Enumerable.Empty<string>());
        }

        var headers = jsonArray.First().Keys.ToList();
        var rows = jsonArray.Select(obj =>
            obj.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ValueKind == JsonValueKind.Null ? string.Empty : kvp.Value.ToString()
            )
        ).ToList();

        return new DataPipeline(rows, headers);
    }

    /// <summary>
    /// Create pipeline from XML file (simple flat structure: root/row/column)
    /// </summary>
    public static async Task<DataPipeline> FromXmlAsync(string path, string rowElement = "row")
    {
        var xmlContent = await File.ReadAllTextAsync(path);
        var doc = XDocument.Parse(xmlContent);
        var rowElements = doc.Descendants(rowElement).ToList();

        if (!rowElements.Any())
        {
            return new DataPipeline(Enumerable.Empty<Dictionary<string, string>>(), Enumerable.Empty<string>());
        }

        var headers = rowElements.First()
            .Elements()
            .Select(e => e.Name.LocalName)
            .ToList();

        var rows = rowElements.Select(rowElem =>
        {
            var row = new Dictionary<string, string>();
            foreach (var elem in rowElem.Elements())
            {
                row[elem.Name.LocalName] = elem.Value;
            }
            return row;
        }).ToList();

        return new DataPipeline(rows, headers);
    }

    /// <summary>
    /// Concatenate multiple CSV files matching a pattern into a single DataPipeline
    /// </summary>
    /// <param name="pattern">File pattern (e.g., "*.csv", "kemp-*.csv")</param>
    /// <param name="directory">Directory containing files (default: current directory)</param>
    /// <param name="hasHeader">Whether files have header rows (default: true)</param>
    /// <param name="addSourceColumn">Add column tracking source filename (default: false)</param>
    /// <param name="sourceColumnName">Name for source tracking column (default: "SourceFile")</param>
    /// <returns>DataPipeline with concatenated data from all matching files</returns>
    public static async Task<DataPipeline> ConcatCsvAsync(
        string pattern,
        string? directory = null,
        bool hasHeader = true,
        bool addSourceColumn = false,
        string sourceColumnName = "SourceFile")
    {
        // Get target directory
        var targetDir = string.IsNullOrEmpty(directory) ? Directory.GetCurrentDirectory() : directory;

        // Find all matching files and sort alphabetically for predictable order
        var files = Directory.GetFiles(targetDir, pattern)
            .OrderBy(f => f)
            .ToList();

        if (!files.Any())
        {
            // No files matched - return empty pipeline with warning
            Console.WriteLine($"Warning: No files matched pattern '{pattern}' in directory '{targetDir}'");
            return new DataPipeline(Enumerable.Empty<Dictionary<string, string>>(), Enumerable.Empty<string>());
        }

        var allRows = new List<Dictionary<string, string>>();
        List<string>? headers = null;

        foreach (var filePath in files)
        {
            var fileName = Path.GetFileName(filePath);

            try
            {
                using var reader = new StreamReader(filePath);
                using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = hasHeader
                });

                await csv.ReadAsync();
                csv.ReadHeader();
                var currentHeaders = csv.HeaderRecord?.ToList() ?? new List<string>();

                // First file: establish schema
                if (headers == null)
                {
                    headers = new List<string>(currentHeaders);  // Create copy, not reference

                    // Add source column to headers if requested
                    if (addSourceColumn && !headers.Contains(sourceColumnName))
                    {
                        headers.Add(sourceColumnName);
                    }
                }
                else
                {
                    // Subsequent files: validate headers match
                    if (!currentHeaders.SequenceEqual(headers.Where(h => h != sourceColumnName)))
                    {
                        throw new InvalidOperationException(
                            $"Header mismatch in file '{fileName}'. " +
                            $"Expected: [{string.Join(", ", headers.Where(h => h != sourceColumnName))}], " +
                            $"Found: [{string.Join(", ", currentHeaders)}]");
                    }
                }

                // Read and concatenate rows
                while (await csv.ReadAsync())
                {
                    var row = new Dictionary<string, string>();

                    // Read actual data columns from CSV
                    foreach (var header in currentHeaders)
                    {
                        row[header] = csv.GetField(header) ?? string.Empty;
                    }

                    // Add source column if requested (not from CSV, generated)
                    if (addSourceColumn)
                    {
                        row[sourceColumnName] = fileName;
                    }

                    allRows.Add(row);
                }
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new InvalidOperationException(
                    $"Error reading file '{fileName}': {ex.Message}", ex);
            }
        }

        return new DataPipeline(allRows, headers ?? new List<string>());
    }

    #endregion

    #region Properties

    public int RowCount => _rows.Count;
    public IReadOnlyList<string> ColumnNames => _columnNames.AsReadOnly();

    #endregion

    #region Transformation Methods

    /// <summary>
    /// Add a computed column
    /// </summary>
    public DataPipeline AddColumn(string columnName, Func<Dictionary<string, string>, string> valueSelector)
    {
        if (!_columnNames.Contains(columnName))
        {
            _columnNames.Add(columnName);
        }

        foreach (var row in _rows)
        {
            row[columnName] = valueSelector(row);
        }

        return this;
    }

    /// <summary>
    /// Remove columns
    /// </summary>
    public DataPipeline RemoveColumns(params string[] columnNames)
    {
        foreach (var colName in columnNames)
        {
            _columnNames.Remove(colName);
            foreach (var row in _rows)
            {
                row.Remove(colName);
            }
        }

        return this;
    }

    /// <summary>
    /// Rename a column
    /// </summary>
    public DataPipeline RenameColumn(string oldName, string newName)
    {
        var index = _columnNames.IndexOf(oldName);
        if (index >= 0)
        {
            _columnNames[index] = newName;
        }

        foreach (var row in _rows)
        {
            if (row.TryGetValue(oldName, out var value))
            {
                row.Remove(oldName);
                row[newName] = value;
            }
        }

        return this;
    }

    /// <summary>
    /// Filter rows by predicate
    /// </summary>
    public DataPipeline FilterRows(Func<Dictionary<string, string>, bool> predicate)
    {
        var filteredRows = _rows.Where(predicate).ToList();
        return new DataPipeline(filteredRows, _columnNames);
    }

    /// <summary>
    /// Normalize numeric columns (Min-Max or Z-Score)
    /// </summary>
    public DataPipeline Normalize(string[] columns, NormalizationMethod method, double minValue = 0, double maxValue = 1)
    {
        var stats = CalculateColumnStats(columns);

        foreach (var row in _rows)
        {
            foreach (var col in columns)
            {
                if (!row.TryGetValue(col, out var valueStr) || !double.TryParse(valueStr, out var value))
                {
                    continue;
                }

                var (min, max, mean, stdDev) = stats[col];
                double normalized;

                if (method == NormalizationMethod.MinMax)
                {
                    var range = max - min;
                    normalized = Math.Abs(range) < 1e-12
                        ? minValue
                        : ((value - min) / range) * (maxValue - minValue) + minValue;
                }
                else // ZScore
                {
                    normalized = Math.Abs(stdDev) < 1e-12
                        ? mean
                        : (value - mean) / stdDev;
                }

                row[col] = normalized.ToString("G");
            }
        }

        return this;
    }

    /// <summary>
    /// Fill missing values
    /// </summary>
    public DataPipeline FillMissing(string[] columns, FillMethod method, string? constantValue = null)
    {
        foreach (var col in columns)
        {
            var validValues = _rows
                .Select(row => row.TryGetValue(col, out var v) ? v : string.Empty)
                .Where(v => !string.IsNullOrWhiteSpace(v) && double.TryParse(v, out _))
                .Select(double.Parse)
                .ToList();

            if (!validValues.Any())
            {
                continue;
            }

            string fillValue = method switch
            {
                FillMethod.Mean => validValues.Average().ToString("G"),
                FillMethod.Median => CalculateMedian(validValues).ToString("G"),
                FillMethod.Constant when constantValue != null => constantValue,
                _ => "0"
            };

            foreach (var row in _rows)
            {
                if (!row.TryGetValue(col, out var value) || string.IsNullOrWhiteSpace(value))
                {
                    row[col] = fillValue;
                }
            }
        }

        return this;
    }

    /// <summary>
    /// Parse DateTime column from string format
    /// </summary>
    /// <param name="columnName">Column to parse</param>
    /// <param name="format">DateTime format (e.g., "yyyy-MM-dd HH:mm", "yyyy-MM-dd")</param>
    /// <param name="outputFormat">Output format (default: ISO 8601 "yyyy-MM-dd HH:mm:ss")</param>
    /// <returns>DataPipeline for chaining</returns>
    public DataPipeline ParseDateTime(string columnName, string format, string outputFormat = "yyyy-MM-dd HH:mm:ss")
    {
        foreach (var row in _rows)
        {
            if (row.TryGetValue(columnName, out var valueStr) && !string.IsNullOrWhiteSpace(valueStr))
            {
                if (DateTime.TryParseExact(valueStr, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    row[columnName] = dt.ToString(outputFormat);
                }
            }
        }

        return this;
    }

    /// <summary>
    /// Parse Korean time format (오전/오후 H:mm:ss) to DateTime
    /// </summary>
    /// <param name="columnName">Source column with Korean time string</param>
    /// <param name="targetColumnName">Target column for parsed DateTime</param>
    /// <param name="baseDate">Base date to use (default: 2000-01-01)</param>
    /// <returns>DataPipeline for chaining</returns>
    public DataPipeline ParseKoreanTime(
        string columnName,
        string targetColumnName,
        DateTime? baseDate = null)
    {
        var baseDt = baseDate ?? new DateTime(2000, 1, 1);

        // Add target column if not exists
        if (!_columnNames.Contains(targetColumnName))
        {
            _columnNames.Add(targetColumnName);
        }

        foreach (var row in _rows)
        {
            if (row.TryGetValue(columnName, out var timeStr) && !string.IsNullOrWhiteSpace(timeStr))
            {
                try
                {
                    var dt = ParseKoreanTimeString(timeStr.Trim(), baseDt);
                    row[targetColumnName] = dt.ToString("yyyy-MM-dd HH:mm:ss");
                }
                catch (FormatException)
                {
                    // Leave empty or keep original - depending on requirements
                    row[targetColumnName] = string.Empty;
                }
            }
            else
            {
                row[targetColumnName] = string.Empty;
            }
        }

        return this;
    }

    /// <summary>
    /// Parse Korean time string to DateTime
    /// </summary>
    private static DateTime ParseKoreanTimeString(string timeStr, DateTime baseDate)
    {
        int hour, minute, second;

        if (timeStr.StartsWith("오전"))  // AM
        {
            var timePart = timeStr.Substring(2).Trim();  // "9:01:18"
            var parts = timePart.Split(':');

            if (parts.Length != 3)
            {
                throw new FormatException(
                    $"Invalid Korean time format: '{timeStr}'. Expected format: '오전 H:mm:ss' or '오후 H:mm:ss'");
            }

            hour = int.Parse(parts[0]);
            minute = int.Parse(parts[1]);
            second = int.Parse(parts[2]);

            // 12 AM = midnight (00:00)
            if (hour == 12)
            {
                hour = 0;
            }
        }
        else if (timeStr.StartsWith("오후"))  // PM
        {
            var timePart = timeStr.Substring(2).Trim();  // "2:15:30"
            var parts = timePart.Split(':');

            if (parts.Length != 3)
            {
                throw new FormatException(
                    $"Invalid Korean time format: '{timeStr}'. Expected format: '오전 H:mm:ss' or '오후 H:mm:ss'");
            }

            hour = int.Parse(parts[0]);
            minute = int.Parse(parts[1]);
            second = int.Parse(parts[2]);

            // 12 PM = noon (12:00), other PM hours add 12
            if (hour != 12)
            {
                hour += 12;
            }
        }
        else
        {
            throw new FormatException(
                $"Invalid Korean time format: '{timeStr}'. Must start with '오전' (AM) or '오후' (PM)");
        }

        return new DateTime(baseDate.Year, baseDate.Month, baseDate.Day, hour, minute, second);
    }

    /// <summary>
    /// Parse Excel numeric date to DateTime
    /// </summary>
    /// <param name="columnName">Column containing Excel numeric dates</param>
    /// <param name="outputFormat">Output format (default: ISO 8601 "yyyy-MM-dd HH:mm:ss")</param>
    /// <returns>DataPipeline for chaining</returns>
    public DataPipeline ParseExcelDate(string columnName, string outputFormat = "yyyy-MM-dd")
    {
        var excelEpoch = new DateTime(1899, 12, 30); // Excel date origin

        foreach (var row in _rows)
        {
            if (row.TryGetValue(columnName, out var valueStr) && double.TryParse(valueStr, out var excelDate))
            {
                var dt = excelEpoch.AddDays(excelDate);
                row[columnName] = dt.ToString(outputFormat);
            }
        }

        return this;
    }

    /// <summary>
    /// Extract date/time features from DateTime column
    /// </summary>
    /// <param name="columnName">DateTime column to extract from</param>
    /// <param name="features">Features to extract (flags)</param>
    /// <param name="removeOriginal">Remove original DateTime column (default: false)</param>
    /// <returns>DataPipeline for chaining</returns>
    public DataPipeline ExtractDateFeatures(string columnName, DateFeatures features, bool removeOriginal = false)
    {
        var newColumns = new List<string>();

        if (features.HasFlag(DateFeatures.Year)) newColumns.Add($"{columnName}_Year");
        if (features.HasFlag(DateFeatures.Month)) newColumns.Add($"{columnName}_Month");
        if (features.HasFlag(DateFeatures.Day)) newColumns.Add($"{columnName}_Day");
        if (features.HasFlag(DateFeatures.Hour)) newColumns.Add($"{columnName}_Hour");
        if (features.HasFlag(DateFeatures.Minute)) newColumns.Add($"{columnName}_Minute");
        if (features.HasFlag(DateFeatures.DayOfWeek)) newColumns.Add($"{columnName}_DayOfWeek");
        if (features.HasFlag(DateFeatures.DayOfYear)) newColumns.Add($"{columnName}_DayOfYear");
        if (features.HasFlag(DateFeatures.WeekOfYear)) newColumns.Add($"{columnName}_WeekOfYear");
        if (features.HasFlag(DateFeatures.Quarter)) newColumns.Add($"{columnName}_Quarter");

        // Add new columns to column names
        foreach (var col in newColumns)
        {
            if (!_columnNames.Contains(col))
            {
                _columnNames.Add(col);
            }
        }

        foreach (var row in _rows)
        {
            if (row.TryGetValue(columnName, out var valueStr) && DateTime.TryParse(valueStr, out var dt))
            {
                if (features.HasFlag(DateFeatures.Year)) row[$"{columnName}_Year"] = dt.Year.ToString();
                if (features.HasFlag(DateFeatures.Month)) row[$"{columnName}_Month"] = dt.Month.ToString();
                if (features.HasFlag(DateFeatures.Day)) row[$"{columnName}_Day"] = dt.Day.ToString();
                if (features.HasFlag(DateFeatures.Hour)) row[$"{columnName}_Hour"] = dt.Hour.ToString();
                if (features.HasFlag(DateFeatures.Minute)) row[$"{columnName}_Minute"] = dt.Minute.ToString();
                if (features.HasFlag(DateFeatures.DayOfWeek)) row[$"{columnName}_DayOfWeek"] = ((int)dt.DayOfWeek).ToString();
                if (features.HasFlag(DateFeatures.DayOfYear)) row[$"{columnName}_DayOfYear"] = dt.DayOfYear.ToString();
                if (features.HasFlag(DateFeatures.Quarter)) row[$"{columnName}_Quarter"] = ((dt.Month - 1) / 3 + 1).ToString();

                if (features.HasFlag(DateFeatures.WeekOfYear))
                {
                    var calendar = CultureInfo.CurrentCulture.Calendar;
                    var weekOfYear = calendar.GetWeekOfYear(dt, CalendarWeekRule.FirstDay, DayOfWeek.Monday);
                    row[$"{columnName}_WeekOfYear"] = weekOfYear.ToString();
                }
            }
        }

        if (removeOriginal)
        {
            RemoveColumns(columnName);
        }

        return this;
    }

    #endregion

    #region Window Operations

    /// <summary>
    /// Resample time-series data by grouping into time windows and aggregating
    /// </summary>
    /// <param name="timeColumn">Column containing DateTime values</param>
    /// <param name="window">Window size (e.g., "5T" = 5 minutes, "1H" = 1 hour, "1D" = 1 day)</param>
    /// <param name="method">Aggregation method (Mean, Min, Max, Sum, Count, Std)</param>
    /// <param name="targetColumns">Columns to aggregate (numeric columns only)</param>
    /// <returns>New DataPipeline with aggregated rows</returns>
    public DataPipeline Resample(string timeColumn, string window, AggregationMethod method, string[] targetColumns)
    {
        // Parse window specification (e.g., "5T" = 5 minutes)
        var match = System.Text.RegularExpressions.Regex.Match(window, @"^(\d+)([THD])$");
        if (!match.Success)
        {
            throw new ArgumentException($"Invalid window format: {window}. Use format like '5T' (5 minutes), '1H' (1 hour), or '1D' (1 day).");
        }

        var windowValue = int.Parse(match.Groups[1].Value);
        var windowUnit = match.Groups[2].Value;

        // Convert to TimeSpan
        TimeSpan windowSpan = windowUnit switch
        {
            "T" => TimeSpan.FromMinutes(windowValue), // T = minutes
            "H" => TimeSpan.FromHours(windowValue),   // H = hours
            "D" => TimeSpan.FromDays(windowValue),    // D = days
            _ => throw new ArgumentException($"Unknown window unit: {windowUnit}")
        };

        // Group rows by time windows
        var timeGroups = new Dictionary<DateTime, List<Dictionary<string, string>>>();

        foreach (var row in _rows)
        {
            if (!row.TryGetValue(timeColumn, out var timeStr) || !DateTime.TryParse(timeStr, out var dt))
            {
                continue; // Skip rows with invalid DateTime
            }

            // Round down to window boundary
            var ticks = dt.Ticks / windowSpan.Ticks;
            var windowStart = new DateTime(ticks * windowSpan.Ticks);

            if (!timeGroups.ContainsKey(windowStart))
            {
                timeGroups[windowStart] = new List<Dictionary<string, string>>();
            }

            timeGroups[windowStart].Add(row);
        }

        // Aggregate each group
        var aggregatedRows = new List<Dictionary<string, string>>();

        foreach (var (windowStart, groupRows) in timeGroups.OrderBy(kvp => kvp.Key))
        {
            var aggregatedRow = new Dictionary<string, string>
            {
                [timeColumn] = windowStart.ToString("yyyy-MM-dd HH:mm:ss")
            };

            foreach (var col in targetColumns)
            {
                var values = groupRows
                    .Select(row => row.TryGetValue(col, out var v) ? v : string.Empty)
                    .Where(v => !string.IsNullOrWhiteSpace(v) && double.TryParse(v, out _))
                    .Select(double.Parse)
                    .ToList();

                if (values.Any())
                {
                    aggregatedRow[col] = CalculateAggregation(values, method).ToString("G");
                }
            }

            aggregatedRows.Add(aggregatedRow);
        }

        // Create new column names (time column + target columns)
        var newColumnNames = new List<string> { timeColumn };
        newColumnNames.AddRange(targetColumns);

        return new DataPipeline(aggregatedRows, newColumnNames);
    }

    /// <summary>
    /// Apply rolling window aggregation over rows
    /// </summary>
    /// <param name="windowSize">Number of rows in the rolling window</param>
    /// <param name="method">Aggregation method (Mean, Min, Max, Sum, Count, Std)</param>
    /// <param name="targetColumns">Columns to aggregate (numeric columns only)</param>
    /// <param name="outputSuffix">Suffix to add to output column names (default: "_rolling")</param>
    /// <returns>DataPipeline with new rolling aggregation columns added</returns>
    public DataPipeline Rolling(int windowSize, AggregationMethod method, string[] targetColumns, string? outputSuffix = "_rolling")
    {
        if (windowSize < 1)
        {
            throw new ArgumentException("Window size must be at least 1.", nameof(windowSize));
        }

        outputSuffix ??= "_rolling";

        // Add new column names
        var newColumns = targetColumns.Select(col => $"{col}{outputSuffix}").ToList();
        foreach (var col in newColumns)
        {
            if (!_columnNames.Contains(col))
            {
                _columnNames.Add(col);
            }
        }

        // Process each row with rolling window
        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];

            foreach (var col in targetColumns)
            {
                var windowStart = Math.Max(0, i - windowSize + 1);
                var windowEnd = i + 1;

                var values = _rows
                    .Skip(windowStart)
                    .Take(windowEnd - windowStart)
                    .Select(r => r.TryGetValue(col, out var v) ? v : string.Empty)
                    .Where(v => !string.IsNullOrWhiteSpace(v) && double.TryParse(v, out _))
                    .Select(double.Parse)
                    .ToList();

                if (values.Any())
                {
                    row[$"{col}{outputSuffix}"] = CalculateAggregation(values, method).ToString("G");
                }
                else
                {
                    row[$"{col}{outputSuffix}"] = string.Empty;
                }
            }
        }

        return this;
    }

    #endregion

    #region Output Methods

    /// <summary>
    /// Get all values from a column
    /// </summary>
    public IEnumerable<string> GetColumn(string columnName)
    {
        return _rows.Select(row => row.TryGetValue(columnName, out var value) ? value : string.Empty);
    }

    /// <summary>
    /// Get immutable snapshot of current data
    /// </summary>
    public DataFrame ToDataFrame()
    {
        return new DataFrame(_rows, _columnNames);
    }

    /// <summary>
    /// Write to CSV file (only at the end of pipeline)
    /// </summary>
    public async Task ToCsvAsync(string path, bool hasHeader = true)
    {
        await using var writer = new StreamWriter(path);
        await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = hasHeader
        });

        // Write header
        if (hasHeader)
        {
            foreach (var col in _columnNames)
            {
                csv.WriteField(col);
            }
            await csv.NextRecordAsync();
        }

        // Write rows
        foreach (var row in _rows)
        {
            foreach (var col in _columnNames)
            {
                csv.WriteField(row.TryGetValue(col, out var value) ? value : string.Empty);
            }
            await csv.NextRecordAsync();
        }
    }

    /// <summary>
    /// Write to Excel file (.xlsx)
    /// </summary>
    public async Task ToExcelAsync(string path, bool hasHeader = true, string sheetName = "Sheet1")
    {
        await ExcelUtils.WriteExcelFileAsync(path, _rows, _columnNames, hasHeader, sheetName);
    }

    /// <summary>
    /// Write to JSON file (array of objects)
    /// </summary>
    public async Task ToJsonAsync(string path, bool indented = true)
    {
        var jsonData = _rows.Select(row =>
        {
            var dict = new Dictionary<string, object?>();
            foreach (var col in _columnNames)
            {
                dict[col] = row.TryGetValue(col, out var value) ? value : null;
            }
            return dict;
        }).ToList();

        var options = new JsonSerializerOptions
        {
            WriteIndented = indented
        };

        var jsonString = JsonSerializer.Serialize(jsonData, options);
        await File.WriteAllTextAsync(path, jsonString);
    }

    /// <summary>
    /// Write to XML file (simple flat structure: root/row/column)
    /// </summary>
    public async Task ToXmlAsync(string path, string rootElement = "data", string rowElement = "row")
    {
        var root = new XElement(rootElement);

        foreach (var row in _rows)
        {
            var rowElem = new XElement(rowElement);
            foreach (var col in _columnNames)
            {
                var value = row.TryGetValue(col, out var v) ? v : string.Empty;
                rowElem.Add(new XElement(col, value));
            }
            root.Add(rowElem);
        }

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
        await using var writer = new StreamWriter(path);
        await writer.WriteAsync(doc.ToString());
    }

    #endregion

    #region Helper Methods

    private Dictionary<string, (double min, double max, double mean, double stdDev)> CalculateColumnStats(string[] columns)
    {
        var stats = new Dictionary<string, (double, double, double, double)>();

        foreach (var col in columns)
        {
            var values = _rows
                .Select(row => row.TryGetValue(col, out var v) ? v : string.Empty)
                .Where(v => double.TryParse(v, out _))
                .Select(double.Parse)
                .ToList();

            if (!values.Any())
            {
                stats[col] = (0, 0, 0, 0);
                continue;
            }

            var min = values.Min();
            var max = values.Max();
            var mean = values.Average();
            var variance = values.Average(v => Math.Pow(v - mean, 2));
            var stdDev = Math.Sqrt(variance);

            stats[col] = (min, max, mean, stdDev);
        }

        return stats;
    }

    private static double CalculateMedian(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2
            : sorted[mid];
    }


    private static double CalculateAggregation(List<double> values, AggregationMethod method)
    {
        return method switch
        {
            AggregationMethod.Mean => values.Average(),
            AggregationMethod.Min => values.Min(),
            AggregationMethod.Max => values.Max(),
            AggregationMethod.Sum => values.Sum(),
            AggregationMethod.Count => values.Count,
            AggregationMethod.Std => Math.Sqrt(values.Average(v => Math.Pow(v - values.Average(), 2))),
            _ => throw new ArgumentException($"Unknown aggregation method: {method}")
        };
    }

    #endregion
}

public enum FillMethod
{
    Mean,
    Median,
    Mode,
    Forward,
    Backward,
    Constant
}

[Flags]
public enum DateFeatures
{
    None = 0,
    Year = 1 << 0,           // 1
    Month = 1 << 1,          // 2
    Day = 1 << 2,            // 4
    Hour = 1 << 3,           // 8
    Minute = 1 << 4,         // 16
    DayOfWeek = 1 << 5,      // 32
    DayOfYear = 1 << 6,      // 64
    WeekOfYear = 1 << 7,     // 128
    Quarter = 1 << 8,        // 256
    All = Year | Month | Day | Hour | Minute | DayOfWeek | DayOfYear | WeekOfYear | Quarter
}
