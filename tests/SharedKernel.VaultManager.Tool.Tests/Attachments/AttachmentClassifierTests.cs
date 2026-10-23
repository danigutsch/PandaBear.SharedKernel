using System.Text;
using PandaBear.VaultManager.Tool.Attachments;

namespace PandaBear.VaultManager.Tool.Tests.Attachments;

public sealed class AttachmentClassifierTests
{
    [Theory]
    [MemberData(nameof(AttachmentSamples.All), MemberType = typeof(AttachmentSamples))]
    public void Classify_detects_kind_by_magic_bytes_then_extension(string fileName, byte[] content, string expected)
    {
        // Act
        var kind = AttachmentClassifier.Classify(content, complete: true, fileName);

        // Assert
        kind.ShouldBe(Enum.Parse<AttachmentKind>(expected));
    }

    [Fact]
    public void Classify_uses_magic_bytes_before_extension()
    {
        // Act
        var kind = AttachmentClassifier.Classify("%PDF-1.4"u8.ToArray(), complete: true, "looks-like.kdbx");

        // Assert
        kind.ShouldBe(AttachmentKind.Pdf);
    }

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

    [Fact]
    public void Classify_treats_text_with_nul_as_binary()
    {
        // Act
        var kind = AttachmentClassifier.Classify("abc\0def"u8.ToArray(), complete: true, "file.txt");

        // Assert
        kind.ShouldBe(AttachmentKind.Binary);
    }

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
