using System.Text;
using PandaBear.VaultManager.Tool.Attachments;

namespace PandaBear.VaultManager.Tool.Tests.Attachments;

public sealed class SafeArchiveTests
{
    [Fact]
    public void Open_lists_file_entries_with_measured_size_and_kind()
    {
        byte[] zip = TestZip.Create(("files/a", TestZip.Utf8("hello")), ("files/b.pdf", "%PDF-1.7"u8.ToArray()));

        using var archive = SafeArchive.Open(new MemoryStream(zip), ArchiveLimits.Default);

        archive.Entries.ShouldBe([new ArchiveEntryInfo("files/a", 5, AttachmentKind.Text), new ArchiveEntryInfo("files/b.pdf", 8, AttachmentKind.Pdf)]);
        archive.Find("files/a").ShouldNotBeNull().Size.ShouldBe(5);
        archive.Find("missing").ShouldBeNull();
    }

    [Fact]
    public void ReadAllBytes_returns_entry_content_and_text_reading_skips_binaries()
    {
        byte[] zip = TestZip.Create(("a.txt", TestZip.Utf8("hello\nworld")), ("b.bin", [0, 1, 2]));
        using var archive = SafeArchive.Open(new MemoryStream(zip), ArchiveLimits.Default);

        archive.ReadAllBytes(archive.Find("b.bin")!).ShouldBe([0, 1, 2]);
        archive.ReadText(archive.Find("a.txt")!).ShouldNotBeNull().Text.ShouldBe("hello\nworld");
        archive.ReadText(archive.Find("b.bin")!).ShouldBeNull();
    }

    [Fact]
    public void Open_rejects_compression_ratio_bomb_measured_on_actual_bytes()
    {
        byte[] zip = TestZip.Create(("bomb.txt", new byte[20 * 1024 * 1024]));

        var action = () => SafeArchive.Open(new MemoryStream(zip), ArchiveLimits.Default);

        var exception = action.ShouldThrow<InvalidDataException>();

        exception.Message.ShouldContain("ratio", StringComparison.Ordinal);
    }

    [Fact]
    public void Open_rejects_more_entries_than_the_limit()
    {
        var entries = Enumerable.Range(0, 1001).Select(index => ($"f{index}", new byte[] { 1 })).ToArray();
        byte[] zip = TestZip.Create(entries);

        var action = () => SafeArchive.Open(new MemoryStream(zip), ArchiveLimits.Default);

        var exception = action.ShouldThrow<InvalidDataException>();

        exception.Message.ShouldContain("entries", StringComparison.Ordinal);
    }

    [Fact]
    public void Open_rejects_expanded_size_above_the_limit()
    {
        byte[] zip = TestZip.Create(("a", TestZip.RandomBytes(600)), ("b", TestZip.RandomBytes(600)));
        var limits = ArchiveLimits.Default with { MaxExpandedBytes = 1000 };

        var action = () => SafeArchive.Open(new MemoryStream(zip), limits);

        var exception = action.ShouldThrow<InvalidDataException>();

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
        byte[] zip = TestZip.Create((path, [1]));

        var action = () => SafeArchive.Open(new MemoryStream(zip), ArchiveLimits.Default);

        var exception = action.ShouldThrow<InvalidDataException>();

        exception.Message.ShouldNotContain("evil", StringComparison.Ordinal);
    }

    [Fact]
    public void Open_allows_nesting_up_to_two_levels()
    {
        byte[] level2 = TestZip.Create(("deep.txt", [0x61]));
        byte[] level1 = TestZip.Create(("level2.zip", level2));
        byte[] zip = TestZip.Create(("level1.zip", level1));

        using var archive = SafeArchive.Open(new MemoryStream(zip), ArchiveLimits.Default);

        archive.Entries.ShouldHaveSingleItem().Kind.ShouldBe(AttachmentKind.Zip);
    }

    [Fact]
    public void Open_rejects_nesting_deeper_than_two_levels()
    {
        byte[] level3 = TestZip.Create(("deep.txt", [0x61]));
        byte[] level2 = TestZip.Create(("level3.zip", level3));
        byte[] level1 = TestZip.Create(("level2.zip", level2));
        byte[] zip = TestZip.Create(("level1.zip", level1));

        var action = () => SafeArchive.Open(new MemoryStream(zip), ArchiveLimits.Default);

        var exception = action.ShouldThrow<InvalidDataException>();

        exception.Message.ShouldContain("nested", StringComparison.Ordinal);
    }

    [Fact]
    public void Open_rejects_traversal_inside_nested_archive()
    {
        byte[] inner = TestZip.Create(("../evil.txt", [1]));
        byte[] zip = TestZip.Create(("inner.zip", inner));

        var action = () => SafeArchive.Open(new MemoryStream(zip), ArchiveLimits.Default);

        action.ShouldThrow<InvalidDataException>();
    }

    [Fact]
    public void Open_rejects_data_that_is_not_a_zip()
    {
        var action = () => SafeArchive.Open(new MemoryStream(Encoding.UTF8.GetBytes("not a zip")), ArchiveLimits.Default);

        action.ShouldThrow<InvalidDataException>();
    }

    [Fact]
    public void Default_limits_match_the_design()
    {
        var limits = ArchiveLimits.Default;

        limits.MaxEntries.ShouldBe(1000);
        limits.MaxExpandedBytes.ShouldBe(100L * 1024 * 1024);
        limits.MaxCompressionRatio.ShouldBe(100);
        limits.MaxNestingDepth.ShouldBe(2);
    }
}
