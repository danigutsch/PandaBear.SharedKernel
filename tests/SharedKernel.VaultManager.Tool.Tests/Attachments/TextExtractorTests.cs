using System.Text;
using PandaBear.VaultManager.Tool.Attachments;

namespace PandaBear.VaultManager.Tool.Tests.Attachments;

public sealed class TextExtractorTests
{
    [Fact]
    public void Extract_returns_text_without_truncation_when_within_limits()
    {
        var text = TextExtractor.Extract(Encoding.UTF8.GetBytes("\uFEFFline one\nline two"));

        text.Text.ShouldBe("line one\nline two");
        text.Truncated.ShouldBeFalse();
        text.TruncatedLines.ShouldBe(0);
    }

    [Fact]
    public void Extract_caps_file_size_and_reports_truncation()
    {
        var text = TextExtractor.Extract(Encoding.UTF8.GetBytes("abcdefghij"), maxBytes: 4, maxLineLength: 100);

        text.Text.ShouldBe("abcd");
        text.Truncated.ShouldBeTrue();
    }

    [Fact]
    public void Extract_caps_line_length_and_counts_truncated_lines()
    {
        var text = TextExtractor.Extract(Encoding.UTF8.GetBytes("short\n0123456789\r\nok"), maxBytes: 100, maxLineLength: 4);

        text.Text.ShouldBe("shor\n0123\r\nok");
        text.TruncatedLines.ShouldBe(2);
    }

    [Fact]
    public void Extract_default_limits_are_ten_mebibytes_and_sixty_four_kibibytes_per_line()
    {
        TextExtractor.MaxBytes.ShouldBe(10 * 1024 * 1024);
        TextExtractor.MaxLineLength.ShouldBe(64 * 1024);
    }

    [Fact]
    public void ToString_of_extracted_text_never_contains_content()
    {
        var text = TextExtractor.Extract(Encoding.UTF8.GetBytes("secret-value"));

        text.ToString().ShouldNotContain("secret-value", StringComparison.Ordinal);
    }
}
