using PandaBear.Testing;
using PandaBear.VaultManager.Tool.Formats.Bitwarden;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Tests.Formats.Bitwarden;

/// <summary>Tests the reader for Bitwarden zip exports and its matching of attachment folders to items.</summary>
public sealed class BitwardenZipReaderTests : IDisposable
{
    private readonly TemporaryDirectory temporaryDirectory = new();

    private string DirectoryPath => temporaryDirectory.Path;

    /// <inheritdoc />
    public void Dispose() => temporaryDirectory.Dispose();

    [Fact]
    public void Read_attaches_files_to_items_whose_sanitized_name_matches_unambiguously()
    {
        // Act
        var result = BitwardenZipReader.Read(BitwardenZipFixture.Write(DirectoryPath));

        // Assert
        var items = result.Export.Items;
        items.ShouldHaveSingleItem(item => item.Name == "Report").Attachments
            .ShouldBe([new AttachmentRef("scan.pdf", 4, "attachments/Report/scan.pdf")]);
        items.ShouldHaveSingleItem(item => item.Name == "a/b: c").Attachments
            .ShouldBe([new AttachmentRef("key.txt", 3, "attachments/a_b_ c/key.txt")]);
        items.ShouldHaveSingleItem(item => item.Origin.OriginalId == "i1").Origin.SourceFile.ShouldBe("export.zip");
    }

    [Fact]
    public void Read_keeps_the_folders_that_the_export_declares()
    {
        // Arrange
        string path = Path.Combine(DirectoryPath, "folders.zip");
        File.WriteAllBytes(path, TestZip.Create(("data.json", TestZip.Utf8("""
            {"encrypted":false,"folders":[{"id":"f1","name":"Work"},{"id":"f2","name":"Empty"}],"items":[
              {"id":"i1","type":2,"name":"Note","folderId":"f1","secureNote":{"type":0}}
            ]}
            """))));

        // Act
        var result = BitwardenZipReader.Read(path);

        // Assert
        result.Export.Folders.ShouldBe(["Work", "Empty"]);
        result.Export.Items.ShouldHaveSingleItem().FolderPath.ShouldBe("Work");
    }

    [Fact]
    public void Read_does_not_guess_when_several_items_share_a_name()
    {
        // Act
        var result = BitwardenZipReader.Read(BitwardenZipFixture.Write(DirectoryPath));

        // Assert
        result.Export.Items.Where(item => item.Name == "Dup").ShouldAllSatisfy(item => item.Attachments.ShouldBeEmpty());
        result.Warnings.ShouldContain(warning => warning.Contains("\"Dup\"", StringComparison.Ordinal));
        result.Warnings.ShouldContain(warning => warning.Contains("\"Dup_1\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Read_warns_about_attachment_folders_without_matching_item()
    {
        // Act
        var result = BitwardenZipReader.Read(BitwardenZipFixture.Write(DirectoryPath));

        // Assert
        result.Warnings.ShouldContain(warning => warning.Contains("\"Orphan\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Read_lists_the_files_of_folders_that_match_no_item_or_several_items()
    {
        // Act
        var result = BitwardenZipReader.Read(BitwardenZipFixture.Write(DirectoryPath));

        // Assert
        var folders = result.UnassignedAttachmentFolders;
        folders.Select(folder => (folder.SourceFile, folder.FolderName, folder.MatchingItemCount)).ShouldBe(
        [
            ("export.zip", "Dup", 2),
            ("export.zip", "Dup_1", 2),
            ("export.zip", "Orphan", 0),
        ]);
        folders.SelectMany(folder => folder.Files).ShouldBe(
        [
            new AttachmentRef("one.txt", 1, "attachments/Dup/one.txt"),
            new AttachmentRef("two.txt", 1, "attachments/Dup_1/two.txt"),
            new AttachmentRef("lost.txt", 1, "attachments/Orphan/lost.txt"),
        ]);
    }

    [Fact]
    public void Read_zip_without_data_json_throws_invalid_data()
    {
        // Arrange
        string path = Path.Combine(DirectoryPath, "empty.zip");
        File.WriteAllBytes(path, TestZip.Create(("other.txt", [1])));
        var action = () => BitwardenZipReader.Read(path);

        // Act, Assert
        action.ShouldThrow<InvalidDataException>();
    }
}
