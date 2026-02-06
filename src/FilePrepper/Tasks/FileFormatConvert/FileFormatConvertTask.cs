using System.Text.Json;
using System.Xml.Linq;
using CsvHelper;

namespace FilePrepper.Tasks.FileFormatConvert;

public class FileFormatConvertTask : BaseTask<FileFormatConvertOption>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public FileFormatConvertTask(ILogger<FileFormatConvertTask> logger) : base(logger)
    {
    }

    protected override Task<List<Dictionary<string, string>>> ProcessRecordsAsync(
        List<Dictionary<string, string>> records)
    {
        if (!Options.IsValid)
        {
            throw new ValidationException("Invalid options");
        }
        return Task.FromResult(records);
    }

    protected override async Task WriteOutputAsync(
        string outputPath,
        IEnumerable<string> headers,
        IEnumerable<Dictionary<string, string>> records)
    {
        var recordsList = records.ToList();
        var encoding = Options.OutputEncoding ?? Encoding.UTF8;

        try
        {
            switch (Options.TargetFormat)
            {
                case FileFormat.CSV:
                    await WriteDelimitedFileAsync(recordsList, outputPath, ",", encoding);
                    break;
                case FileFormat.TSV:
                    await WriteDelimitedFileAsync(recordsList, outputPath, "\t", encoding);
                    break;
                case FileFormat.PSV:
                    await WriteDelimitedFileAsync(recordsList, outputPath, "|", encoding);
                    break;
                case FileFormat.JSON:
                    await WriteJsonFileAsync(recordsList, outputPath, encoding);
                    break;
                case FileFormat.XML:
                    await WriteXmlFileAsync(recordsList, outputPath, encoding);
                    break;
                default:
                    throw new ArgumentException($"Unsupported format: {Options.TargetFormat}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while writing output file: {Error}", ex.Message);
            throw;
        }
    }

    private async Task WriteDelimitedFileAsync(
        List<Dictionary<string, string>> records,
        string outputPath,
        string delimiter,
        Encoding encoding)
    {
        var config = CsvUtils.GetDefaultConfiguration();
        config.Delimiter = delimiter;
        config.HasHeaderRecord = Options.HasHeader;

        await using var writer = new StreamWriter(outputPath, false, encoding);
        await using var csv = new CsvWriter(writer, config);

        if (records.Count == 0)
        {
            return;
        }

        // Write headers
        if (Options.HasHeader)
        {
            foreach (var header in records[0].Keys)
            {
                csv.WriteField(header);
            }
            csv.NextRecord();
        }

        // Write data
        foreach (var record in records)
        {
            foreach (var value in record.Values)
            {
                csv.WriteField(value);
            }
            csv.NextRecord();
        }
    }

    private async Task WriteJsonFileAsync(
        List<Dictionary<string, string>> records,
        string outputPath,
        Encoding encoding)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = Options.PrettyPrint,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(records, options);
        await File.WriteAllTextAsync(outputPath, json, encoding);
    }

    private async Task WriteXmlFileAsync(
        List<Dictionary<string, string>> records,
        string outputPath,
        Encoding encoding)
    {
        var root = new XElement(Options.RootElementName);
        var document = new XDocument(
            new XDeclaration("1.0", encoding.WebName, "yes"),
            root);

        foreach (var record in records)
        {
            var item = new XElement(Options.ItemElementName);
            foreach (var (key, value) in record)
            {
                item.Add(new XElement(key, value));
            }
            root.Add(item);
        }

        await using var writer = new StreamWriter(outputPath, false, encoding);
        if (Options.PrettyPrint)
        {
            await writer.WriteAsync(document.ToString());
        }
        else
        {
            await writer.WriteAsync(document.ToString(SaveOptions.DisableFormatting));
        }
    }
}