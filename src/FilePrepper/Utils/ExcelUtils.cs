using System.Data;
using System.Text;
using ExcelDataReader;
using OfficeOpenXml;

namespace FilePrepper.Utils;

public static class ExcelUtils
{
    static ExcelUtils()
    {
        // Required for ExcelDataReader to work with encodings
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // Set EPPlus license for version 8+ (NonCommercial usage)
        ExcelPackage.License.SetNonCommercialPersonal("FilePrepper");
    }

    /// <summary>
    /// Read Excel file (.xls, .xlsx) and convert to records
    /// </summary>
    public static async Task<(List<Dictionary<string, string>> records, List<string> headers)> ReadExcelFileAsync(
        string filePath,
        bool hasHeader = true,
        string? worksheetName = null,
        int worksheetIndex = 0)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Excel file not found: {filePath}");
        }

        var records = new List<Dictionary<string, string>>();
        var headers = new List<string>();

        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        // Auto-detect format based on file extension
        using var reader = Path.GetExtension(filePath).ToLowerInvariant() == ".xls"
            ? ExcelReaderFactory.CreateBinaryReader(stream)
            : ExcelReaderFactory.CreateOpenXmlReader(stream);

        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration
            {
                UseHeaderRow = hasHeader
            }
        });

        // Get the specified worksheet
        DataTable? table = null;
        if (!string.IsNullOrEmpty(worksheetName))
        {
            table = dataSet.Tables[worksheetName];
            if (table == null)
            {
                throw new InvalidOperationException($"Worksheet '{worksheetName}' not found");
            }
        }
        else
        {
            if (worksheetIndex >= dataSet.Tables.Count)
            {
                throw new InvalidOperationException($"Worksheet index {worksheetIndex} out of range");
            }
            table = dataSet.Tables[worksheetIndex];
        }

        // Get headers
        if (hasHeader)
        {
            foreach (DataColumn column in table.Columns)
            {
                headers.Add(column.ColumnName);
            }
        }
        else
        {
            for (int i = 0; i < table.Columns.Count; i++)
            {
                headers.Add(i.ToString());
            }
        }

        // Read data rows
        foreach (DataRow row in table.Rows)
        {
            var record = new Dictionary<string, string>();
            bool hasData = false;

            for (int i = 0; i < table.Columns.Count && i < headers.Count; i++)
            {
                var cellValue = row[i]?.ToString() ?? string.Empty;
                record[headers[i]] = cellValue;

                if (!string.IsNullOrWhiteSpace(cellValue))
                {
                    hasData = true;
                }
            }

            // Only add non-empty rows
            if (hasData)
            {
                records.Add(record);
            }
        }

        return await Task.FromResult((records, headers));
    }

    /// <summary>
    /// Get worksheet names from Excel file
    /// </summary>
    public static List<string> GetWorksheetNames(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Excel file not found: {filePath}");
        }

        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        using var reader = Path.GetExtension(filePath).ToLowerInvariant() == ".xls"
            ? ExcelReaderFactory.CreateBinaryReader(stream)
            : ExcelReaderFactory.CreateOpenXmlReader(stream);

        var dataSet = reader.AsDataSet();
        return dataSet.Tables.Cast<DataTable>().Select(t => t.TableName).ToList();
    }

    /// <summary>
    /// Check if file is Excel format
    /// </summary>
    public static bool IsExcelFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension == ".xls" || extension == ".xlsx";
    }

    /// <summary>
    /// Write data to Excel file (.xlsx) using EPPlus
    /// </summary>
    public static async Task WriteExcelFileAsync(
        string filePath,
        List<Dictionary<string, string>> records,
        List<string> headers,
        bool hasHeader = true,
        string sheetName = "Sheet1")
    {

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Exists)
        {
            fileInfo.Delete();
        }

        using var package = new ExcelPackage(fileInfo);
        var worksheet = package.Workbook.Worksheets.Add(sheetName);

        int currentRow = 1;

        // Write header
        if (hasHeader)
        {
            for (int col = 0; col < headers.Count; col++)
            {
                worksheet.Cells[currentRow, col + 1].Value = headers[col];
            }
            worksheet.Cells[currentRow, 1, currentRow, headers.Count].Style.Font.Bold = true;
            currentRow++;
        }

        // Write data rows
        foreach (var record in records)
        {
            for (int col = 0; col < headers.Count; col++)
            {
                var value = record.TryGetValue(headers[col], out var v) ? v : string.Empty;
                worksheet.Cells[currentRow, col + 1].Value = value;
            }
            currentRow++;
        }

        // Auto-fit columns
        worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

        await package.SaveAsync();
    }
}
