using System.Text;
using PandaBear.VaultManager.Tool.Attachments;

namespace PandaBear.VaultManager.Tool.Tests.Attachments;

public sealed class AttachmentClassifierTests
{
    [Theory]
    [MemberData(nameof(AttachmentSamples.All), MemberType = typeof(AttachmentSamples))]
    public void Classify_detects_kind_by_magic_bytes_then_extension(string fileName, byte[] content, string expected)
    {
        var kind = AttachmentClassifier.Classify(content, complete: true, fileName);

        kind.ShouldBe(Enum.Parse<AttachmentKind>(expected));
    }

    [Fact]
    public void Classify_uses_magic_bytes_before_extension()
    {
        var kind = AttachmentClassifier.Classify("%PDF-1.4"u8.ToArray(), complete: true, "looks-like.kdbx");

        kind.ShouldBe(AttachmentKind.Pdf);
    }

    [Fact]
    public void Classify_accepts_text_whose_head_ends_inside_a_multibyte_character()
    {
        byte[] head = [.. Encoding.UTF8.GetBytes("abc"), 0xE2, 0x82];

        var kind = AttachmentClassifier.Classify(head, complete: false, "partial.txt");

        kind.ShouldBe(AttachmentKind.Text);
    }

    [Fact]
    public void Classify_treats_text_with_nul_as_binary()
    {
        var kind = AttachmentClassifier.Classify("abc\0def"u8.ToArray(), complete: true, "file.txt");

        kind.ShouldBe(AttachmentKind.Binary);
    }

    [Fact]
    public void IsText_is_true_only_for_textual_kinds()
    {
        AttachmentClassifier.IsText(AttachmentKind.Text).ShouldBeTrue();
        AttachmentClassifier.IsText(AttachmentKind.PrivateKeyText).ShouldBeTrue();
        AttachmentClassifier.IsText(AttachmentKind.EthereumKeystore).ShouldBeTrue();
        AttachmentClassifier.IsText(AttachmentKind.Binary).ShouldBeFalse();
        AttachmentClassifier.IsText(AttachmentKind.KeePass).ShouldBeFalse();
    }
}
