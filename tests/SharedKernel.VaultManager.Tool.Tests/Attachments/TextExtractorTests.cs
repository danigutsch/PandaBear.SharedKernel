using System.Text;
using PandaBear.VaultManager.Tool.Attachments;

namespace PandaBear.VaultManager.Tool.Tests.Attachments;

/// <summary>Tests extraction of text from attachments within size and line limits.</summary>
public sealed class TextExtractorTests
{
    /// <summary>Verifies that text within the limits is returned whole and not marked truncated.</summary>
    [Fact]
    public void Extract_returns_text_without_truncation_when_within_limits()
    {
        // Act
        var text = TextExtractor.Extract(Encoding.UTF8.GetBytes("\uFEFFline one\nline two"));

        // Assert
        text.Text.ShouldBe("line one\nline two");
        text.Truncated.ShouldBeFalse();
        text.TruncatedLines.ShouldBe(0);
    }

    /// <summary>Verifies that text above the size limit is cut at the limit and reported as truncated.</summary>
    [Fact]
    public void Extract_caps_file_size_and_reports_truncation()
    {
        // Act
        var text = TextExtractor.Extract(Encoding.UTF8.GetBytes("abcdefghij"), maxBytes: 4, maxLineLength: 100);

        // Assert
        text.Text.ShouldBe("abcd");
        text.Truncated.ShouldBeTrue();
    }

    /// <summary>Verifies that overlong lines are cut and counted.</summary>
    [Fact]
    public void Extract_caps_line_length_and_counts_truncated_lines()
    {
        // Act
        var text = TextExtractor.Extract(Encoding.UTF8.GetBytes("short\n0123456789\r\nok"), maxBytes: 100, maxLineLength: 4);

        // Assert
        text.Text.ShouldBe("shor\n0123\r\nok");
        text.TruncatedLines.ShouldBe(2);
    }

    /// <summary>Verifies that the default limits are 10 MiB per file and 64 KiB per line.</summary>
    [Fact]
    public void Extract_default_limits_are_ten_mebibytes_and_sixty_four_kibibytes_per_line()
    {
        // Act
        var maxBytes = TextExtractor.MaxBytes;
        var maxLineLength = TextExtractor.MaxLineLength;

        // Assert
        maxBytes.ShouldBe(10 * 1024 * 1024);
        maxLineLength.ShouldBe(64 * 1024);
    }

    /// <summary>Verifies that the string form of extracted text never includes the extracted content.</summary>
    [Fact]
    public void ToString_of_extracted_text_never_contains_content()
    {
        // Arrange
        var text = TextExtractor.Extract(Encoding.UTF8.GetBytes("secret-value"));

        // Act
        var description = text.ToString();

        // Assert
        description.ShouldNotContain("secret-value", StringComparison.Ordinal);
    }
}
