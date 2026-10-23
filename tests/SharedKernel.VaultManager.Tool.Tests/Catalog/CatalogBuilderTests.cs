using PandaBear.VaultManager.Tool.Catalog;
using PandaBear.VaultManager.Tool.Merge;
using static PandaBear.VaultManager.Tool.Tests.Catalog.CatalogFixture;

namespace PandaBear.VaultManager.Tool.Tests.Catalog;

public sealed class CatalogBuilderTests
{
    [Fact]
    public void Build_shows_metadata_and_withholds_every_secret_value()
    {
        var built = CatalogBuilder.Build(Vault(Login()), "abc", MergeDecisions.None);

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
        var built = CatalogBuilder.Build(Vault(Login()), "abc", MergeDecisions.None);

        var pending = built.PendingNotes.ShouldHaveSingleItem();
        pending.OriginKey.ShouldBe(Key);
        pending.Decision.ShouldBe(NotesDecision.Pending);
        pending.ItemName.ShouldBe("GitHub");
        built.Counts.PendingNotes.ShouldBe(1);
    }

    [Fact]
    public void Build_shows_notes_only_when_the_decisions_file_shares_them()
    {
        var shared = CatalogBuilder.Build(Vault(Login()), "abc", Notes(NotesDecision.Share));
        var hidden = CatalogBuilder.Build(Vault(Login()), "abc", Notes(NotesDecision.Hidden));

        shared.Catalog.Items.ShouldHaveSingleItem().Notes.ShouldBe(new CatalogNotes(NotesDecision.Share, NotesText));
        shared.PendingNotes.ShouldBeEmpty();
        hidden.Catalog.Items.ShouldHaveSingleItem().Notes.ShouldBe(new CatalogNotes(NotesDecision.Hidden));
    }

    [Fact]
    public void Build_withholds_a_user_name_that_holds_a_secret_finding()
    {
        var item = Login() with { Login = Login().Login! with { Username = Totp } };

        CatalogBuilder.Build(Vault(item), "abc", MergeDecisions.None).Catalog.Items.ShouldHaveSingleItem().Info.Username.ShouldBeNull();
    }
}
