using System.Text;

namespace FilePrepper.Utils;

/// <summary>
/// Detects text file encoding with focus on Korean character encodings.
/// Supports UTF-8, CP949 (Korean Windows), EUC-KR (Korean legacy).
/// </summary>
public static class EncodingDetector
{
    /// <summary>
    /// Detects the encoding of a file by analyzing byte patterns.
    /// Returns Encoding.UTF8 by default.
    /// </summary>
    public static Encoding DetectEncoding(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
        {
            return Encoding.UTF8;
        }

        byte[] buffer = new byte[Math.Min(64 * 1024, fileInfo.Length)];
        int bytesRead;

        using (var fs = File.OpenRead(filePath))
        {
            bytesRead = fs.Read(buffer, 0, buffer.Length);
        }

        if (bytesRead == 0)
        {
            return Encoding.UTF8;
        }

        // Check for BOM (Byte Order Mark)
        if (bytesRead >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
        {
            return Encoding.UTF8;
        }

        if (bytesRead >= 2)
        {
            if (buffer[0] == 0xFF && buffer[1] == 0xFE)
                return Encoding.Unicode; // UTF-16 LE
            if (buffer[0] == 0xFE && buffer[1] == 0xFF)
                return Encoding.BigEndianUnicode; // UTF-16 BE
        }

        // No BOM - analyze byte patterns
        var span = new ReadOnlySpan<byte>(buffer, 0, bytesRead);

        // Check if valid UTF-8
        if (IsValidUtf8(span))
        {
            return Encoding.UTF8;
        }

        // Check for Korean encodings (CP949/EUC-KR)
        if (HasKoreanEncodingPatterns(span))
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(949); // CP949
        }

        // Fallback to UTF-8
        return Encoding.UTF8;
    }

    /// <summary>
    /// Resolves an encoding string to an Encoding instance.
    /// "auto" triggers auto-detection from the file.
    /// </summary>
    public static Encoding ResolveEncoding(string filePath, string encoding)
    {
        if (string.Equals(encoding, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return DetectEncoding(filePath);
        }

        // Handle common aliases
        var normalized = encoding.ToLowerInvariant().Replace("-", "");
        return normalized switch
        {
            "cp949" or "euckr" => GetKoreanEncoding(),
            "utf8" => Encoding.UTF8,
            "utf16" or "utf16le" => Encoding.Unicode,
            "utf16be" => Encoding.BigEndianUnicode,
            _ => Encoding.GetEncoding(encoding)
        };
    }

    private static Encoding GetKoreanEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(949);
    }

    private static bool IsValidUtf8(ReadOnlySpan<byte> data)
    {
        int i = 0;
        while (i < data.Length)
        {
            byte b = data[i];

            if (b <= 0x7F)
            {
                i++;
            }
            else if ((b & 0xE0) == 0xC0)
            {
                // Truncated at the detection-buffer boundary is incomplete, not invalid — a large
                // UTF-8 file whose 64KB sample cuts a multibyte char must not fail UTF-8 validation
                // (else it falls through to the Korean check and is misdetected as CP949).
                if (i + 1 >= data.Length) break;
                if ((data[i + 1] & 0xC0) != 0x80) return false;
                i += 2;
            }
            else if ((b & 0xF0) == 0xE0)
            {
                if (i + 2 >= data.Length) break;
                if ((data[i + 1] & 0xC0) != 0x80 ||
                    (data[i + 2] & 0xC0) != 0x80)
                    return false;
                i += 3;
            }
            else if ((b & 0xF8) == 0xF0)
            {
                if (i + 3 >= data.Length) break;
                if ((data[i + 1] & 0xC0) != 0x80 ||
                    (data[i + 2] & 0xC0) != 0x80 ||
                    (data[i + 3] & 0xC0) != 0x80)
                    return false;
                i += 4;
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasKoreanEncodingPatterns(ReadOnlySpan<byte> data)
    {
        int koreanByteSequences = 0;
        int invalidSequences = 0;
        int i = 0;

        while (i < data.Length)
        {
            byte b = data[i];

            if (b <= 0x7F)
            {
                i++;
            }
            else if (b >= 0x81 && b <= 0xFE)
            {
                if (i + 1 < data.Length)
                {
                    byte trail = data[i + 1];
                    if ((trail >= 0x41 && trail <= 0x5A) ||
                        (trail >= 0x61 && trail <= 0x7A) ||
                        (trail >= 0x81 && trail <= 0xFE))
                    {
                        koreanByteSequences++;
                        i += 2;
                    }
                    else
                    {
                        invalidSequences++;
                        i++;
                    }
                }
                else
                {
                    invalidSequences++;
                    i++;
                }
            }
            else
            {
                invalidSequences++;
                i++;
            }
        }

        if (koreanByteSequences == 0) return false;

        float totalSequences = koreanByteSequences + invalidSequences;
        float confidence = koreanByteSequences / totalSequences;
        return confidence > 0.5f;
    }
}
