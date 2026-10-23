using PandaBear.VaultManager.Tool.Catalog;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;
using static PandaBear.VaultManager.Tool.Tests.Catalog.CatalogFixture;

namespace PandaBear.VaultManager.Tool.Tests.Catalog;

public sealed class CatalogApplierTests
{
    [Fact]
    public void Apply_of_an_unedited_catalog_changes_nothing()
    {
        var vault = Vault(Login());
        var catalog = CatalogBuilder.Build(vault, "abc", MergeDecisions.None).Catalog;

        var result = CatalogApplier.Apply(vault, catalog, MergeDecisions.None);

        result.Problems.ShouldBeEmpty();
        result.ChangedItems.ShouldBe(0);
        var item = result.Vault.ShouldNotBeNull().Items.ShouldHaveSingleItem();
        item.Notes.ShouldBe(NotesText);
        var login = item.Login.ShouldNotBeNull();
        login.Password.ShouldBe(Password);
        login.Totp.ShouldBe(Totp);
        login.Uris.ShouldBe(Login().Login!.Uris);
        item.Fields.ShouldBe(Login().Fields);
        result.Vault.Folders.ShouldBe(["Archive"]);
    }

    [Fact]
    public void Apply_takes_the_edited_parts_and_keeps_every_secret()
    {
        var vault = Vault(Login());
        var entry = CatalogBuilder.Build(vault, "abc", Notes(NotesDecision.Share)).Catalog.Items[0];
        var edited = entry with
        {
            Name = "GitHub (work)",
            Folder = "Work/Code",
            AdditionalFolders = ["Personal", "Archive"],
            Favorite = true,
            Notes = new CatalogNotes(NotesDecision.Share, "Organization account"),
            Uris = [entry.Uris[1], new CatalogUri("https://github.com", UriMatch.Host)],
            Fields = [new CatalogField("Plan", "Enterprise"), new CatalogField("Owner", "IT")],
        };

        var result = CatalogApplier.Apply(vault, CatalogWith(vault, [edited], ["Archive", "Empty"]), Notes(NotesDecision.Share));

        result.Problems.ShouldBeEmpty();
        result.ChangedItems.ShouldBe(1);
        var item = result.Vault.ShouldNotBeNull().Items.ShouldHaveSingleItem();
        item.Name.ShouldBe("GitHub (work)");
        item.FolderPath.ShouldBe("Work/Code");
        item.Favorite.ShouldBeTrue();
        item.Notes.ShouldBe("Organization account");
        var login = item.Login.ShouldNotBeNull();
        login.Password.ShouldBe(Password);
        login.Totp.ShouldBe(Totp);
        login.Uris.ShouldBe([new LoginUri(MaskedUri), new LoginUri("https://github.com", UriMatch.Host)]);
        item.PasswordHistory.ShouldBe(Login().PasswordHistory);
        item.Fields.ShouldBe(
        [
            new CustomField("Plan", "Enterprise", CustomFieldType.Text),
            new CustomField("API key", HiddenValue, CustomFieldType.Hidden),
            new CustomField("Backup", SeedInField, CustomFieldType.Text),
            new CustomField("Owner", "IT", CustomFieldType.Text),
            new CustomField(AdditionalFolders.FieldName, "Personal\nArchive", CustomFieldType.Text),
        ]);
        result.Vault.Folders.ShouldBe(["Archive", "Empty"]);
    }

    [Fact]
    public void Apply_moves_notes_the_user_hides_into_a_hidden_field()
    {
        var vault = Vault(Login());
        var catalog = CatalogBuilder.Build(vault, "abc", Notes(NotesDecision.Hidden)).Catalog;

        var item = CatalogApplier.Apply(vault, catalog, Notes(NotesDecision.Hidden)).Vault.ShouldNotBeNull().Items.ShouldHaveSingleItem();

        item.Notes.ShouldBeNull();
        item.Fields.ShouldContain(new CustomField(CatalogApplier.HiddenNotesField, NotesText, CustomFieldType.Hidden));
    }

    [Fact]
    public void Apply_keeps_withheld_notes_when_the_decision_changed_after_the_catalog()
    {
        var vault = Vault(Login());
        var catalog = CatalogBuilder.Build(vault, "abc", MergeDecisions.None).Catalog;

        var item = CatalogApplier.Apply(vault, catalog, Notes(NotesDecision.Share)).Vault.ShouldNotBeNull().Items.ShouldHaveSingleItem();

        item.Notes.ShouldBe(NotesText);
    }

    [Theory]
    [InlineData("withheld notes text")]
    [InlineData("notes shared only in the catalog")]
    [InlineData("changed masked address")]
    [InlineData("removed masked address")]
    [InlineData("unknown key")]
    [InlineData("missing item")]
    [InlineData("empty name")]
    public void Apply_rejects_a_catalog_that_changes_what_it_may_not(string change)
    {
        var vault = Vault(Login());
        var entry = CatalogBuilder.Build(vault, "abc", MergeDecisions.None).Catalog.Items[0];
        CatalogItem[] items = change switch
        {
            "withheld notes text" => [entry with { Notes = new CatalogNotes(NotesDecision.Pending, "replaced") }],
            "notes shared only in the catalog" => [entry with { Notes = new CatalogNotes(NotesDecision.Share, "replaced") }],
            "changed masked address" => [entry with { Uris = [entry.Uris[0], entry.Uris[1] with { Masked = "https://evil.example.com" }] }],
            "removed masked address" => [entry with { Uris = [entry.Uris[0]] }],
            "unknown key" => [entry, entry with { Key = "bitwarden|vault.json|other" }],
            "missing item" => [],
            _ => [entry with { Name = " " }],
        };

        var result = CatalogApplier.Apply(vault, CatalogWith(vault, items), MergeDecisions.None);

        result.Vault.ShouldBeNull();
        result.Problems.ShouldNotBeEmpty();
        result.Problems.ShouldAllSatisfy(problem =>
        {
            problem.ShouldNotContain(NotesText, StringComparison.Ordinal);
            problem.ShouldNotContain("fixture-uri", StringComparison.Ordinal);
        });
    }
}
