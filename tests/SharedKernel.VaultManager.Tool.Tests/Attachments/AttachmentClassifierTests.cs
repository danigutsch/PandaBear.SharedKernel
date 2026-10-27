using System.Text;
using PandaBear.VaultManager.Tool.Attachments;

namespace PandaBear.VaultManager.Tool.Tests.Attachments;

/// <summary>Tests classification of attachments by content and file name.</summary>
public sealed class AttachmentClassifierTests
{
    /// <summary>Verifies that each sample attachment is classified as its expected kind.</summary>
    [Theory]
    [MemberData(nameof(AttachmentSamples.All), MemberType = typeof(AttachmentSamples))]
    public void Classify_detects_kind_by_magic_bytes_then_extension(string fileName, byte[] content, string expected)
    {
        // Act
        var kind = AttachmentClassifier.Classify(content, complete: true, fileName);

        // Assert
        kind.ShouldBe(Enum.Parse<AttachmentKind>(expected));
    }

    /// <summary>Verifies that recognized magic bytes win over a misleading file extension.</summary>
    [Fact]
    public void Classify_uses_magic_bytes_before_extension()
    {
        // Act
        var kind = AttachmentClassifier.Classify("%PDF-1.4"u8.ToArray(), complete: true, "looks-like.kdbx");

        // Assert
        kind.ShouldBe(AttachmentKind.Pdf);
    }

    /// <summary>Verifies that UTF-8 text is still text when the inspected head ends in the middle of a multibyte character.</summary>
    [Fact]
    public void Classify_accepts_text_whose_head_ends_inside_a_multibyte_character()
    {
        // Arrange
        byte[] head = [.. Encoding.UTF8.GetBytes("abc"), 0xE2, 0x82];

        // Act
        var kind = AttachmentClassifier.Classify(head, complete: false, "partial.txt");

        // Assert
        kind.ShouldBe(AttachmentKind.Text);
    }

    /// <summary>Verifies that content with a NUL byte is classified as binary.</summary>
    [Fact]
    public void Classify_treats_text_with_nul_as_binary()
    {
        // Act
        var kind = AttachmentClassifier.Classify("abc\0def"u8.ToArray(), complete: true, "file.txt");

        // Assert
        kind.ShouldBe(AttachmentKind.Binary);
    }

    /// <summary>Verifies that only the textual attachment kinds count as text.</summary>
    [Fact]
    public void IsText_is_true_only_for_textual_kinds()
    {
        // Act
        var textIsText = AttachmentClassifier.IsText(AttachmentKind.Text);
        var privateKeyTextIsText = AttachmentClassifier.IsText(AttachmentKind.PrivateKeyText);
        var ethereumKeystoreIsText = AttachmentClassifier.IsText(AttachmentKind.EthereumKeystore);
        var binaryIsText = AttachmentClassifier.IsText(AttachmentKind.Binary);
        var keePassIsText = AttachmentClassifier.IsText(AttachmentKind.KeePass);

        // Assert
        textIsText.ShouldBeTrue();
        privateKeyTextIsText.ShouldBeTrue();
        ethereumKeystoreIsText.ShouldBeTrue();
        binaryIsText.ShouldBeFalse();
        keePassIsText.ShouldBeFalse();
    }
}
