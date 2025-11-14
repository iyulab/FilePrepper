using System.CommandLine;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FilePrepper.CLI.Tests;

/// <summary>
/// Base class for CLI command integration tests
/// </summary>
public abstract class CommandTestBase : IDisposable
{
    protected readonly ILoggerFactory LoggerFactory;
    protected readonly string TestDataDirectory;
    private readonly List<string> _tempFiles = new();
    private readonly List<string> _tempDirectories = new();

    protected CommandTestBase()
    {
        LoggerFactory = NullLoggerFactory.Instance;
        TestDataDirectory = Path.Combine(Path.GetTempPath(), $"FilePrepper_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(TestDataDirectory);
        _tempDirectories.Add(TestDataDirectory);
    }

    #region File Operations

    /// <summary>
    /// Creates a test CSV file with the given content
    /// </summary>
    protected string CreateTestCsv(string fileName, string content)
    {
        var filePath = Path.Combine(TestDataDirectory, fileName);
        File.WriteAllText(filePath, content, Encoding.UTF8);
        _tempFiles.Add(filePath);
        return filePath;
    }

    /// <summary>
    /// Creates a test CSV file with headers and rows
    /// </summary>
    protected string CreateTestCsv(string fileName, string[] headers, string[][] rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", headers));

        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",", row));
        }

        return CreateTestCsv(fileName, sb.ToString());
    }

    /// <summary>
    /// Gets a temporary file path in the test directory
    /// </summary>
    protected string GetTempPath(string fileName)
    {
        var filePath = Path.Combine(TestDataDirectory, fileName);
        _tempFiles.Add(filePath);
        return filePath;
    }

    /// <summary>
    /// Reads all lines from a CSV file
    /// </summary>
    protected string[] ReadCsvLines(string filePath)
    {
        File.Exists(filePath).Should().BeTrue($"Output file should exist: {filePath}");
        return File.ReadAllLines(filePath);
    }

    /// <summary>
    /// Reads CSV file and parses into rows
    /// </summary>
    protected List<string[]> ReadCsvRows(string filePath)
    {
        var lines = ReadCsvLines(filePath);
        return lines.Select(line => line.Split(',')).ToList();
    }

    /// <summary>
    /// Reads CSV file with headers
    /// </summary>
    protected (string[] Headers, List<string[]> Rows) ReadCsvWithHeaders(string filePath)
    {
        var rows = ReadCsvRows(filePath);
        rows.Count.Should().BeGreaterThan(0, "CSV file should have at least headers");

        var headers = rows[0];
        var dataRows = rows.Skip(1).ToList();

        return (headers, dataRows);
    }

    /// <summary>
    /// Checks if a file exists
    /// </summary>
    protected bool FileExists(string filePath) => File.Exists(filePath);

    #endregion

    #region Command Execution

    /// <summary>
    /// Executes a command with the given arguments
    /// </summary>
    protected async Task<int> RunCommandAsync(Command command, params string[] args)
    {
        var parseResult = command.Parse(args);
        return await parseResult.InvokeAsync();
    }

    /// <summary>
    /// Executes a command and captures console output
    /// </summary>
    protected async Task<(int ExitCode, string Output, string Error)> RunCommandWithOutputAsync(
        Command command, params string[] args)
    {
        var outputWriter = new StringWriter();
        var errorWriter = new StringWriter();
        var originalOut = Console.Out;
        var originalError = Console.Error;

        try
        {
            Console.SetOut(outputWriter);
            Console.SetError(errorWriter);

            var parseResult = command.Parse(args);
            var exitCode = await parseResult.InvokeAsync();

            return (exitCode, outputWriter.ToString(), errorWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            outputWriter.Dispose();
            errorWriter.Dispose();
        }
    }

    #endregion

    #region Test Data Helpers

    /// <summary>
    /// Creates sample sales data for testing
    /// </summary>
    protected string CreateSampleSalesData(string fileName = "sales.csv")
    {
        var headers = new[] { "Date", "Product", "Quantity", "Price", "Region" };
        var rows = new[]
        {
            new[] { "2024-01-01", "Widget A", "10", "25.50", "North" },
            new[] { "2024-01-02", "Widget B", "5", "30.00", "South" },
            new[] { "2024-01-03", "Widget A", "8", "25.50", "East" },
            new[] { "2024-01-04", "Widget C", "15", "20.00", "North" },
            new[] { "2024-01-05", "Widget B", "12", "30.00", "West" }
        };

        return CreateTestCsv(fileName, headers, rows);
    }

    /// <summary>
    /// Creates sample time series data for lag features
    /// </summary>
    protected string CreateSampleTimeSeriesData(string fileName = "timeseries.csv")
    {
        var headers = new[] { "PartNumber", "Date", "Value", "Status" };
        var rows = new[]
        {
            new[] { "P001", "2024-01-01", "100", "Active" },
            new[] { "P001", "2024-01-02", "110", "Active" },
            new[] { "P001", "2024-01-03", "105", "Active" },
            new[] { "P002", "2024-01-01", "200", "Active" },
            new[] { "P002", "2024-01-02", "210", "Active" },
            new[] { "P002", "2024-01-03", "215", "Active" }
        };

        return CreateTestCsv(fileName, headers, rows);
    }

    /// <summary>
    /// Creates sample data with missing values
    /// </summary>
    protected string CreateSampleDataWithMissingValues(string fileName = "missing.csv")
    {
        var content = """
            Name,Age,Score,City
            Alice,25,85.5,New York
            Bob,,90.0,
            Charlie,35,,Boston
            David,28,78.5,
            Eve,,,Seattle
            """;

        return CreateTestCsv(fileName, content);
    }

    /// <summary>
    /// Creates sample data for merging
    /// </summary>
    protected (string File1, string File2) CreateSampleMergeData()
    {
        var file1Headers = new[] { "ID", "Name", "Department" };
        var file1Rows = new[]
        {
            new[] { "1", "Alice", "Engineering" },
            new[] { "2", "Bob", "Sales" },
            new[] { "3", "Charlie", "Marketing" }
        };

        var file2Headers = new[] { "ID", "Salary", "Years" };
        var file2Rows = new[]
        {
            new[] { "1", "90000", "5" },
            new[] { "2", "75000", "3" },
            new[] { "3", "80000", "4" }
        };

        var file1 = CreateTestCsv("merge1.csv", file1Headers, file1Rows);
        var file2 = CreateTestCsv("merge2.csv", file2Headers, file2Rows);

        return (file1, file2);
    }

    #endregion

    #region Assertions

    /// <summary>
    /// Asserts that a CSV file has the expected number of rows (excluding header)
    /// </summary>
    protected void AssertCsvRowCount(string filePath, int expectedCount)
    {
        var (_, rows) = ReadCsvWithHeaders(filePath);
        rows.Count.Should().Be(expectedCount, $"Expected {expectedCount} rows in {Path.GetFileName(filePath)}");
    }

    /// <summary>
    /// Asserts that a CSV file has the expected number of columns
    /// </summary>
    protected void AssertCsvColumnCount(string filePath, int expectedCount)
    {
        var (headers, _) = ReadCsvWithHeaders(filePath);
        headers.Length.Should().Be(expectedCount, $"Expected {expectedCount} columns in {Path.GetFileName(filePath)}");
    }

    /// <summary>
    /// Asserts that a CSV file contains a specific header
    /// </summary>
    protected void AssertCsvHasHeader(string filePath, string headerName)
    {
        var (headers, _) = ReadCsvWithHeaders(filePath);
        headers.Should().Contain(headerName, $"CSV should contain header '{headerName}'");
    }

    /// <summary>
    /// Asserts that a CSV file contains specific headers
    /// </summary>
    protected void AssertCsvHasHeaders(string filePath, params string[] headerNames)
    {
        var (headers, _) = ReadCsvWithHeaders(filePath);
        foreach (var headerName in headerNames)
        {
            headers.Should().Contain(headerName, $"CSV should contain header '{headerName}'");
        }
    }

    /// <summary>
    /// Asserts that a column contains no empty/null values
    /// </summary>
    protected void AssertColumnHasNoEmptyValues(string filePath, string columnName)
    {
        var (headers, rows) = ReadCsvWithHeaders(filePath);
        var columnIndex = Array.IndexOf(headers, columnName);
        columnIndex.Should().BeGreaterThanOrEqualTo(0, $"Column '{columnName}' should exist");

        foreach (var row in rows)
        {
            if (columnIndex < row.Length)
            {
                row[columnIndex].Should().NotBeNullOrWhiteSpace($"Column '{columnName}' should not have empty values");
            }
        }
    }

    #endregion

    #region Cleanup

    public void Dispose()
    {
        // Clean up temporary files
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        // Clean up temporary directories
        foreach (var directory in _tempDirectories)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    #endregion
}
