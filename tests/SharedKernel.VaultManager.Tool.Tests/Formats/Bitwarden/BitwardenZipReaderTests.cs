using PandaBear.VaultManager.Tool.Formats.Bitwarden;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Tests.Formats.Bitwarden;

public sealed class BitwardenZipReaderTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("vault-manager-bitwarden-").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public void Read_attaches_files_to_items_whose_sanitized_name_matches_unambiguously()
    {
        var result = BitwardenZipReader.Read(BitwardenZipFixture.Write(directory));

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
        string path = Path.Combine(directory, "folders.zip");
        File.WriteAllBytes(path, TestZip.Create(("data.json", TestZip.Utf8("""
            {"encrypted":false,"folders":[{"id":"f1","name":"Work"},{"id":"f2","name":"Empty"}],"items":[
              {"id":"i1","type":2,"name":"Note","folderId":"f1","secureNote":{"type":0}}
            ]}
            """))));

        var result = BitwardenZipReader.Read(path);

        result.Export.Folders.ShouldBe(["Work", "Empty"]);
        result.Export.Items.ShouldHaveSingleItem().FolderPath.ShouldBe("Work");
    }

    [Fact]
    public void Read_does_not_guess_when_several_items_share_a_name()
    {
        var result = BitwardenZipReader.Read(BitwardenZipFixture.Write(directory));

        result.Export.Items.Where(item => item.Name == "Dup").ShouldAllSatisfy(item => item.Attachments.ShouldBeEmpty());
        result.Warnings.ShouldContain(warning => warning.Contains("\"Dup\"", StringComparison.Ordinal));
        result.Warnings.ShouldContain(warning => warning.Contains("\"Dup_1\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Read_warns_about_attachment_folders_without_matching_item()
    {
        var result = BitwardenZipReader.Read(BitwardenZipFixture.Write(directory));

        result.Warnings.ShouldContain(warning => warning.Contains("\"Orphan\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Read_lists_the_files_of_folders_that_match_no_item_or_several_items()
    {
        var result = BitwardenZipReader.Read(BitwardenZipFixture.Write(directory));

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
        string path = Path.Combine(directory, "empty.zip");
        File.WriteAllBytes(path, TestZip.Create(("other.txt", [1])));

        var action = () => BitwardenZipReader.Read(path);

        action.ShouldThrow<InvalidDataException>();
    }
}
