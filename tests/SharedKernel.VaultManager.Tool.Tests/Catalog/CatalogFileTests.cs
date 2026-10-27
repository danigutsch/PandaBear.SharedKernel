using PandaBear.VaultManager.Tool.Catalog;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;
using static PandaBear.VaultManager.Tool.Tests.Catalog.CatalogFixture;

namespace PandaBear.VaultManager.Tool.Tests.Catalog;

/// <summary>Tests reading and writing the catalog file.</summary>
public sealed class CatalogFileTests
{
    /// <summary>Verifies that items without a folder, notes, user name, or addresses read back as written.</summary>
    [Fact]
    public void Catalog_round_trips_items_without_folder_notes_user_name_or_addresses()
    {
        // Arrange
        var note = new VaultItem { Type = VaultItemType.SecureNote, Name = "Wifi", Origin = new ItemOrigin("bitwarden", "vault.json", "item-2", null) };
        var catalog = CatalogBuilder.Build(Vault(Login(), note), "abc", MergeDecisions.None).Catalog;

        // Act
        var reread = CatalogFile.Deserialize(CatalogFile.Serialize(catalog));

        // Assert
        reread.Items.Count.ShouldBe(2);
        reread.Items[1].Folder.ShouldBeNull();
        reread.Items[1].Notes.ShouldBeNull();
        reread.Items[1].Info.Username.ShouldBeNull();
        reread.Items[0].Uris.ShouldBe(catalog.Items[0].Uris);
        reread.Items[0].Notes.ShouldBe(catalog.Items[0].Notes);
    }

    /// <summary>Verifies that a catalog with unknown or missing properties, or another version, is rejected.</summary>
    [Theory]
    [InlineData("""{"version":1,"source":{"file":"v.json","sha256":"a"},"folders":[],"items":[],"extra":1}""")]
    [InlineData("""{"version":2,"source":{"file":"v.json","sha256":"a"},"folders":[],"items":[]}""")]
    [InlineData("""{"version":1,"folders":[],"items":[]}""")]
    public void Catalog_rejects_unknown_missing_properties_and_other_versions(string json)
    {
        // Arrange
        var action = () => CatalogFile.Deserialize(json);

        // Act, Assert
        action.ShouldThrow<InvalidDataException>();
    }
}
