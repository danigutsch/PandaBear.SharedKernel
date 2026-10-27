using PandaBear.VaultManager.Tool.Formats.Bitwarden;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Tests.Formats.Bitwarden;

/// <summary>Tests the reader for Bitwarden zip exports and its matching of attachment folders to items.</summary>
public sealed class BitwardenZipReaderTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("vault-manager-bitwarden-").FullName;

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(directory, recursive: true);

    /// <summary>The Report item and the item named "a/b: c" receive the files of their sanitized attachment folders with size and path, and items carry the zip file name as source.</summary>
    [Fact]
    public void Read_attaches_files_to_items_whose_sanitized_name_matches_unambiguously()
    {
        // Act
        var result = BitwardenZipReader.Read(BitwardenZipFixture.Write(directory));

        // Assert
        var items = result.Export.Items;
        items.ShouldHaveSingleItem(item => item.Name == "Report").Attachments
            .ShouldBe([new AttachmentRef("scan.pdf", 4, "attachments/Report/scan.pdf")]);
        items.ShouldHaveSingleItem(item => item.Name == "a/b: c").Attachments
            .ShouldBe([new AttachmentRef("key.txt", 3, "attachments/a_b_ c/key.txt")]);
        items.ShouldHaveSingleItem(item => item.Origin.OriginalId == "i1").Origin.SourceFile.ShouldBe("export.zip");
    }

    /// <summary>A zip whose data.json declares the folders "Work" and "Empty" keeps both and puts its note in "Work".</summary>
    [Fact]
    public void Read_keeps_the_folders_that_the_export_declares()
    {
        // Arrange
        string path = Path.Combine(directory, "folders.zip");
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

    /// <summary>The two items named "Dup" get no attachments and the result warns about the "Dup" and "Dup_1" folders.</summary>
    [Fact]
    public void Read_does_not_guess_when_several_items_share_a_name()
    {
        // Act
        var result = BitwardenZipReader.Read(BitwardenZipFixture.Write(directory));

        // Assert
        result.Export.Items.Where(item => item.Name == "Dup").ShouldAllSatisfy(item => item.Attachments.ShouldBeEmpty());
        result.Warnings.ShouldContain(warning => warning.Contains("\"Dup\"", StringComparison.Ordinal));
        result.Warnings.ShouldContain(warning => warning.Contains("\"Dup_1\"", StringComparison.Ordinal));
    }

    /// <summary>The "Orphan" attachment folder, which matches no item, produces a warning that names it.</summary>
    [Fact]
    public void Read_warns_about_attachment_folders_without_matching_item()
    {
        // Act
        var result = BitwardenZipReader.Read(BitwardenZipFixture.Write(directory));

        // Assert
        result.Warnings.ShouldContain(warning => warning.Contains("\"Orphan\"", StringComparison.Ordinal));
    }

    /// <summary>The unassigned folders Dup and Dup_1 (two matching items each) and Orphan (none) are listed with their source file and files.</summary>
    [Fact]
    public void Read_lists_the_files_of_folders_that_match_no_item_or_several_items()
    {
        // Act
        var result = BitwardenZipReader.Read(BitwardenZipFixture.Write(directory));

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

    /// <summary>A zip that holds only other.txt and no data.json throws InvalidDataException.</summary>
    [Fact]
    public void Read_zip_without_data_json_throws_invalid_data()
    {
        // Arrange
        string path = Path.Combine(directory, "empty.zip");
        File.WriteAllBytes(path, TestZip.Create(("other.txt", [1])));
        var action = () => BitwardenZipReader.Read(path);

        // Act, Assert
        action.ShouldThrow<InvalidDataException>();
    }
}
