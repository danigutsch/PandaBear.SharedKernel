namespace SharedKernel.Repositories.Tests;

/// <summary>Pins the position that <see cref="JsonVerifier.DescribeInvalidUtf8"/> reports for the first invalid UTF-8 sequence.</summary>
public sealed class JsonVerifierInvalidUtf8Tests
{
    [Theory]
    [MemberData(nameof(Cases))]
    public void DescribeInvalidUtf8_reports_the_line_and_byte_of_the_first_invalid_sequence(byte[] content, string expected)
    {
        // Act
        var message = JsonVerifier.DescribeInvalidUtf8(content);

        // Assert
        message.ShouldBe(expected);
    }

    /// <summary>Gets content and the message expected for it.</summary>
    public static TheoryData<byte[], string> Cases => new()
    {
        { [0xFF], "Invalid UTF-8 at line 1, byte 1." },
        { [(byte)'a', (byte)'b', 0xC3, 0xA9, 0xFF], "Invalid UTF-8 at line 1, byte 5." },
        { [(byte)'a', (byte)'\n', (byte)'b', (byte)'c', 0xC0, 0xAF], "Invalid UTF-8 at line 2, byte 3." },
        { [(byte)'a', (byte)'b', 0xE2, 0x82], "Invalid UTF-8 at line 1, byte 3." },
        { [(byte)'x', 0xED, 0xA0, 0x80], "Invalid UTF-8 at line 1, byte 2." },
        { [(byte)'\n', (byte)'\n', 0x80], "Invalid UTF-8 at line 3, byte 1." },
        { [(byte)'o', (byte)'k'], "Invalid UTF-8 at line 1, byte 3." },
        { [], "Invalid UTF-8 at line 1, byte 1." },
    };
}
