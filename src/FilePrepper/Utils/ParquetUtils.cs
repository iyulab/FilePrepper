using System.Collections;
using System.Globalization;
using System.Text.Json;
using Parquet.Schema;
using Parquet.Serialization;

namespace FilePrepper.Utils;

/// <summary>
/// Reads Apache Parquet files into the same record shape as CSV and Excel input.
/// </summary>
/// <remarks>
/// A table has one level of columns, so nested data is flattened on the way in:
/// <list type="bullet">
/// <item>a struct becomes one column per leaf, named by its dotted path (<c>labels.label</c>);</item>
/// <item>a list or map stays one column, holding its value as JSON text — it has no fixed width to
/// spread over columns, and JSON keeps it recoverable.</item>
/// </list>
/// Values are written in the invariant culture, and a null becomes an empty cell, as a missing CSV
/// value would be.
/// </remarks>
public static class ParquetUtils
{
    public static async Task<(List<Dictionary<string, string>> records, List<string> headers)> ReadParquetFileAsync(
        string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Parquet file not found: {filePath}");

        await using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var result = await ParquetSerializer.DeserializeUntypedAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var headers = new List<string>();
        foreach (var field in result.Schema.Fields)
            CollectHeaders(field, prefix: null, headers);

        var records = new List<Dictionary<string, string>>(result.Data.Count);
        foreach (var row in result.Data)
        {
            var record = new Dictionary<string, string>(headers.Count);
            foreach (var field in result.Schema.Fields)
                Flatten(field, row.TryGetValue(field.Name, out var value) ? value : null, prefix: null, record);
            records.Add(record);
        }

        return (records, headers);
    }

    private static string Join(string? prefix, string name) => prefix is null ? name : $"{prefix}.{name}";

    private static void CollectHeaders(Field field, string? prefix, List<string> headers)
    {
        var name = Join(prefix, field.Name);
        if (field is StructField structField)
        {
            foreach (var child in structField.Fields)
                CollectHeaders(child, name, headers);
        }
        else
        {
            headers.Add(name);
        }
    }

    private static void Flatten(Field field, object? value, string? prefix, Dictionary<string, string> record)
    {
        var name = Join(prefix, field.Name);
        if (field is StructField structField)
        {
            var nested = value as IDictionary<string, object?>;
            foreach (var child in structField.Fields)
            {
                object? childValue = null;
                nested?.TryGetValue(child.Name, out childValue);
                Flatten(child, childValue, name, record);
            }
            return;
        }

        record[name] = Format(value);
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        // A list or map has no fixed width to spread over columns; JSON keeps it recoverable.
        IDictionary or IList when value is not byte[] => JsonSerializer.Serialize(value),
        byte[] bytes => Convert.ToBase64String(bytes),
        DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        float f => f.ToString("R", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };
}
