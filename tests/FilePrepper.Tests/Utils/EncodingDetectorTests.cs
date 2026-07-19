using System.Text;
using FilePrepper.Utils;
using FluentAssertions;
using Xunit;

namespace FilePrepper.Tests.Utils;

/// <summary>
/// <see cref="EncodingDetector"/> 의 바이트-패턴 감지 검증. 특히 64KB 감지 버퍼 경계에서
/// 멀티바이트 UTF-8 시퀀스가 잘릴 때 UTF-8 파일이 CP949 로 오분류되지 않는지(경계 관용).
/// </summary>
public class EncodingDetectorTests
{
    public EncodingDetectorTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static Encoding Cp949 => Encoding.GetEncoding(949);

    private static string WriteBytes(string dir, string fileName, byte[] bytes)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public void DetectEncoding_Utf8_ReturnsUtf8()
    {
        var dir = Path.Combine("TestData", $"Enc_Utf8_{Guid.NewGuid():N}");
        var path = WriteBytes(dir, "utf8.csv", Encoding.UTF8.GetBytes("이름,점수\n홍길동,85"));

        EncodingDetector.DetectEncoding(path).Should().Be(Encoding.UTF8);
    }

    [Fact]
    public void DetectEncoding_Cp949_ReturnsCp949()
    {
        var dir = Path.Combine("TestData", $"Enc_Cp949_{Guid.NewGuid():N}");
        var path = WriteBytes(dir, "cp949.csv", Cp949.GetBytes("이름,점수\n홍길동,85\n김철수,90"));

        EncodingDetector.DetectEncoding(path).CodePage.Should().Be(949);
    }

    /// <summary>
    /// 64KB 감지 버퍼가 3-byte 한글 UTF-8 문자를 정확히 경계에서 자를 때, 잘린 tail 을
    /// "불완전(관용)"이 아니라 "invalid"로 처리하면 UTF-8 검증이 실패하고 CP949 로 오분류된다.
    /// 대용량 UTF-8 파일의 실제 실패 모드 — 경계는 관용해야 한다.
    /// </summary>
    [Fact]
    public void DetectEncoding_Utf8_MultibyteStraddlesDetectionBuffer_StillUtf8()
    {
        // 감지 버퍼 = min(64KB, len). 65534 ASCII + '가'(EA B0 80) → 버퍼(0..65535)는 EA,B0 만
        // 포함하고 80 은 다음 바이트로 밀려 3-byte 시퀀스가 경계에서 잘린다. 뒤에 한글을 더 붙여
        // 파일이 64KB 를 넘게 한다.
        const int bufferSize = 64 * 1024; // 65536
        var head = new byte[bufferSize - 2];        // 65534
        Array.Fill(head, (byte)'a');
        var tail = Encoding.UTF8.GetBytes("가나다");  // 9 bytes, first '가' straddles the boundary
        var bytes = head.Concat(tail).ToArray();     // 65543 > 65536

        var dir = Path.Combine("TestData", $"Enc_Boundary_{Guid.NewGuid():N}");
        var path = WriteBytes(dir, "boundary.csv", bytes);

        // Must NOT be misdetected as CP949 — the file is valid UTF-8, merely truncated in the sample.
        EncodingDetector.DetectEncoding(path).Should().Be(Encoding.UTF8);
    }
}
