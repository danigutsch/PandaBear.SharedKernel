using System.Text;
using PandaBear.VaultManager.Tool.Attachments;

namespace PandaBear.VaultManager.Tool.Tests.Attachments;

/// <summary>Tests safe opening and reading of zip archives and their limits.</summary>
public sealed class SafeArchiveTests
{
    [Fact]
    public void Open_lists_file_entries_with_measured_size_and_kind()
    {
        // Arrange
        byte[] zip = TestZip.Create(("files/a", TestZip.Utf8("hello")), ("files/b.pdf", "%PDF-1.7"u8.ToArray()));

        // Act
        using var archive = SafeArchive.Open(new MemoryStream(zip), ArchiveLimits.Default);

        // Assert
        archive.Entries.ShouldBe([new ArchiveEntryInfo("files/a", 5, AttachmentKind.Text), new ArchiveEntryInfo("files/b.pdf", 8, AttachmentKind.Pdf)]);
        archive.Find("files/a").ShouldNotBeNull().Size.ShouldBe(5);
        archive.Find("missing").ShouldBeNull();
    }

    [Fact]
    public void ReadAllBytes_returns_entry_content_and_text_reading_skips_binaries()
    {
        // Arrange
        byte[] zip = TestZip.Create(("a.txt", TestZip.Utf8("hello\nworld")), ("b.bin", [0, 1, 2]));
        using var archive = SafeArchive.Open(new MemoryStream(zip), ArchiveLimits.Default);

        // Act
        var binaryBytes = archive.ReadAllBytes(archive.Find("b.bin")!);
        var text = archive.ReadText(archive.Find("a.txt")!);
        var binaryText = archive.ReadText(archive.Find("b.bin")!);

        // Assert
        binaryBytes.ShouldBe([0, 1, 2]);
        text.ShouldNotBeNull().Text.ShouldBe("hello\nworld");
        binaryText.ShouldBeNull();
    }

    [Fact]
    public void ComputeSha256_returns_the_digest_of_the_entry_bytes_for_text_and_binaries()
    {
        // Arrange
        byte[] text = TestZip.Utf8("hello\nworld");
        byte[] binary = TestZip.RandomBytes(200_000);
        byte[] zip = TestZip.Create(("a.txt", text), ("b.bin", binary));
        using var archive = SafeArchive.Open(new MemoryStream(zip), ArchiveLimits.Default);

        // Act
        byte[] textDigest = archive.ComputeSha256(archive.Find("a.txt")!);
        byte[] binaryDigest = archive.ComputeSha256(archive.Find("b.bin")!);

        // Assert
        textDigest.ShouldBe(System.Security.Cryptography.SHA256.HashData(text));
        binaryDigest.ShouldBe(System.Security.Cryptography.SHA256.HashData(binary));
    }

    [Fact]
    public void Open_rejects_compression_ratio_bomb_measured_on_actual_bytes()
    {
        // Arrange
        byte[] zip = TestZip.Create(("bomb.txt", new byte[20 * 1024 * 1024]));

        var action = () => SafeArchive.Open(new MemoryStream(zip), ArchiveLimits.Default);

        // Act
        var exception = action.ShouldThrow<InvalidDataException>();

        // Assert
        exception.Message.ShouldContain("ratio", StringComparison.Ordinal);
    }

    [Fact]
    public void Open_rejects_more_entries_than_the_limit()
    {
        // Arrange
        var entries = Enumerable.Range(0, 1001).Select(index => ($"f{index}", new byte[] { 1 })).ToArray();
        byte[] zip = TestZip.Create(entries);

        var action = () => SafeArchive.Open(new MemoryStream(zip), ArchiveLimits.Default);

        // Act
        var exception = action.ShouldThrow<InvalidDataException>();

        // Assert
        exception.Message.ShouldContain("entries", StringComparison.Ordinal);
    }

    [Fact]
    public void Open_rejects_expanded_size_above_the_limit()
    {
        // Arrange
        byte[] zip = TestZip.Create(("a", TestZip.RandomBytes(600)), ("b", TestZip.RandomBytes(600)));
        var limits = ArchiveLimits.Default with
        {
            MaxExpandedBytes = 1000
        };

        var action = () => SafeArchive.Open(new MemoryStream(zip), limits);

        // Act
        var exception = action.ShouldThrow<InvalidDataException>();

        // Assert
        exception.Message.ShouldContain("expanded", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("files/../../evil.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("C:/Windows/evil.txt")]
    [InlineData("c:evil.txt")]
    [InlineData("files\\..\\..\\evil.txt")]
    [InlineData("\\\\server\\share\\evil.txt")]
    public void Open_rejects_entry_paths_that_escape_the_archive(string path)
    {
        // Arrange
        byte[] zip = TestZip.Create((path, [1]));

        var action = () => SafeArchive.Open(new MemoryStream(zip), ArchiveLimits.Default);

        // Act
        var exception = action.ShouldThrow<InvalidDataException>();

        // Assert
        exception.Message.ShouldNotContain("evil", StringComparison.Ordinal);
    }

    [Fact]
    public void Open_allows_nesting_up_to_two_levels()
    {
        // Arrange
        byte[] level2 = TestZip.Create(("deep.txt", [0x61]));
        byte[] level1 = TestZip.Create(("level2.zip", level2));
        byte[] zip = TestZip.Create(("level1.zip", level1));

        // Act
        using var archive = SafeArchive.Open(new MemoryStream(zip), ArchiveLimits.Default);

        // Assert
        archive.Entries.ShouldHaveSingleItem().Kind.ShouldBe(AttachmentKind.Zip);
    }

    [Fact]
    public void Open_rejects_nesting_deeper_than_two_levels()
    {
        // Arrange
        byte[] level3 = TestZip.Create(("deep.txt", [0x61]));
        byte[] level2 = TestZip.Create(("level3.zip", level3));
        byte[] level1 = TestZip.Create(("level2.zip", level2));
        byte[] zip = TestZip.Create(("level1.zip", level1));

        var action = () => SafeArchive.Open(new MemoryStream(zip), ArchiveLimits.Default);

        // Act
        var exception = action.ShouldThrow<InvalidDataException>();

        // Assert
        exception.Message.ShouldContain("nested", StringComparison.Ordinal);
    }

    [Fact]
    public void Open_rejects_traversal_inside_nested_archive()
    {
        // Arrange
        byte[] inner = TestZip.Create(("../evil.txt", [1]));
        byte[] zip = TestZip.Create(("inner.zip", inner));

        var action = () => SafeArchive.Open(new MemoryStream(zip), ArchiveLimits.Default);

        // Act, Assert
        action.ShouldThrow<InvalidDataException>();
    }

    [Fact]
    public void Open_rejects_data_that_is_not_a_zip()
    {
        // Arrange
        var action = () => SafeArchive.Open(new MemoryStream(Encoding.UTF8.GetBytes("not a zip")), ArchiveLimits.Default);

        // Act, Assert
        action.ShouldThrow<InvalidDataException>();
    }

    [Fact]
    public void Default_limits_match_the_design()
    {
        // Act
        var limits = ArchiveLimits.Default;

        // Assert
        limits.MaxEntries.ShouldBe(1000);
        limits.MaxExpandedBytes.ShouldBe(100L * 1024 * 1024);
        limits.MaxCompressionRatio.ShouldBe(100);
        limits.MaxNestingDepth.ShouldBe(2);
    }
}
