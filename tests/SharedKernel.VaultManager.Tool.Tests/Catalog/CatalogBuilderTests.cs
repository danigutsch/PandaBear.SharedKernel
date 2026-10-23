using PandaBear.VaultManager.Tool.Catalog;
using PandaBear.VaultManager.Tool.Merge;
using static PandaBear.VaultManager.Tool.Tests.Catalog.CatalogFixture;

namespace PandaBear.VaultManager.Tool.Tests.Catalog;

public sealed class CatalogBuilderTests
{
    [Fact]
    public void Build_shows_metadata_and_withholds_every_secret_value()
    {
        // Act
        var built = CatalogBuilder.Build(Vault(Login()), "abc", MergeDecisions.None);

        // Assert
        string json = CatalogFile.Serialize(built.Catalog);
        string[] secrets = [Password, Totp, HiddenValue, NotesText, SeedInField, "fixture-uri-secret", "fixture-uri-token", "fixture-catalog-old-password"];
        secrets.ShouldAllSatisfy(secret => json.ShouldNotContain(secret, StringComparison.Ordinal));
        var item = built.Catalog.Items.ShouldHaveSingleItem();
        item.Key.ShouldBe(Key);
        item.Name.ShouldBe("GitHub");
        item.Folder.ShouldBe("Work");
        item.AdditionalFolders.ShouldBe(["Personal"]);
        item.Notes.ShouldBe(new CatalogNotes(NotesDecision.Pending));
        item.Uris.ShouldBe([new CatalogUri("https://github.com/login"), new CatalogUri(Masked: "https://***@example.com/reset?***", Index: 1)]);
        item.Fields.ShouldBe([new CatalogField("Plan", "Team")]);
        item.Info.Username.ShouldBe("alice");
        item.Info.HasPassword.ShouldBeTrue();
        item.Info.HasTotp.ShouldBeTrue();
        item.Info.PasswordHistory.ShouldBe(1);
        item.Info.ProtectedFields.ShouldBe(["API key", "Backup"]);
        built.Catalog.Folders.ShouldBe(["Archive"]);
        built.Catalog.Source.ShouldBe(new CatalogSource("vault.json", "abc"));
    }

    [Fact]
    public void Build_lists_notes_without_a_decision_as_pending_entries()
    {
        // Act
        var built = CatalogBuilder.Build(Vault(Login()), "abc", MergeDecisions.None);

        // Assert
        var pending = built.NotesUpdates.ShouldHaveSingleItem();
        pending.OriginKey.ShouldBe(Key);
        pending.Decision.ShouldBe(NotesDecision.Pending);
        pending.ItemName.ShouldBe("GitHub");
        built.Counts.PendingNotes.ShouldBe(1);
    }

    [Fact]
    public void Build_shows_notes_only_when_the_decisions_file_shares_them()
    {
        // Act
        var shared = CatalogBuilder.Build(Vault(Login()), "abc", Notes(NotesDecision.Share));
        var hidden = CatalogBuilder.Build(Vault(Login()), "abc", Notes(NotesDecision.Hidden));

        // Assert
        shared.Catalog.Items.ShouldHaveSingleItem().Notes.ShouldBe(new CatalogNotes(NotesDecision.Share, NotesText));
        shared.NotesUpdates.ShouldBeEmpty();
        hidden.Catalog.Items.ShouldHaveSingleItem().Notes.ShouldBe(new CatalogNotes(NotesDecision.Hidden));
    }

    [Fact]
    public void Build_withholds_shared_notes_that_changed_since_they_were_shared()
    {
        // Arrange
        var changed = Login() with
        {
            Notes = "fixture notes edited after sharing"
        };

        // Act
        var built = CatalogBuilder.Build(Vault(changed), "abc", Notes(NotesDecision.Share));

        // Assert
        built.Catalog.Items.ShouldHaveSingleItem().Notes.ShouldBe(new CatalogNotes(NotesDecision.Pending));
        var update = built.NotesUpdates.ShouldHaveSingleItem();
        update.Decision.ShouldBe(NotesDecision.Pending);
        update.NotesSha256.ShouldBeNull();
        CatalogFile.Serialize(built.Catalog).ShouldNotContain("edited after sharing", StringComparison.Ordinal);
    }

    [Fact]
    public void Build_binds_a_share_decision_without_a_hash_to_the_current_notes()
    {
        // Arrange
        var decisions = MergeDecisions.None with
        {
            Notes = [new ItemNotesDecision(Key, NotesDecision.Share)]
        };

        // Act
        var built = CatalogBuilder.Build(Vault(Login()), "abc", decisions);

        // Assert
        built.Catalog.Items.ShouldHaveSingleItem().Notes.ShouldBe(new CatalogNotes(NotesDecision.Share, NotesText));
        built.NotesUpdates.ShouldHaveSingleItem().NotesSha256.ShouldBe(ItemNotesDecision.HashOf(NotesText));
    }

    [Fact]
    public void Build_withholds_a_user_name_that_holds_a_secret_finding()
    {
        // Arrange
        var item = Login() with
        {
            Login = Login().Login! with
            {
                Username = Totp
            }
        };

        // Act
        var built = CatalogBuilder.Build(Vault(item), "abc", MergeDecisions.None);

        // Assert
        built.Catalog.Items.ShouldHaveSingleItem().Info.Username.ShouldBeNull();
    }
}
