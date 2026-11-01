using System.Text;

namespace FilePrepper.Utils;

/// <summary>
/// A syllable written whole (NFC) and the same syllable written as its letters (NFD, the form macOS gives file names and many files
/// written there) are the same text and different strings. Compared as strings they were not equal: <c>filter-rows City:Equals:서울</c>
/// kept no row of a file whose cells were written in the letters, <c>replace</c> replaced nothing and <c>conditional</c> took every
/// row for the else, each with exit code 0. Text is put in one form where an input is read, so every task compares like with like.
/// </summary>
internal static class TextForms
{
    public static string Nfc(string value) =>
        value.IsNormalized(NormalizationForm.FormC) ? value : value.Normalize(NormalizationForm.FormC);

    /// <summary>
    /// Puts the column names and the cells of a table read from a file in NFC, in place. A cell that is already in NFC (every ASCII
    /// one) is left as it is and costs one scan.
    /// </summary>
    public static void Canonicalize(List<Dictionary<string, string>> records, List<string> headers)
    {
        var renamed = false;
        for (var i = 0; i < headers.Count; i++)
        {
            var name = Nfc(headers[i]);
            if (!ReferenceEquals(name, headers[i]))
            {
                headers[i] = name;
                renamed = true;
            }
        }

        for (var r = 0; r < records.Count; r++)
        {
            var record = records[r];

            // A row's keys are the table's column names, so they were put in NFC with the names.
            if (renamed)
            {
                var rebuilt = new Dictionary<string, string>(record.Count);
                foreach (var (key, value) in record)
                    rebuilt[Nfc(key)] = Nfc(value);
                records[r] = rebuilt;
                continue;
            }

            foreach (var column in headers)
            {
                if (record.TryGetValue(column, out var value) && !value.IsNormalized(NormalizationForm.FormC))
                    record[column] = value.Normalize(NormalizationForm.FormC);
            }
        }
    }
}
