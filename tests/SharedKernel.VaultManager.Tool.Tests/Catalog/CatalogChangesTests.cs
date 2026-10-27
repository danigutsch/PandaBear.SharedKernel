using PandaBear.VaultManager.Tool.Catalog;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;
using static PandaBear.VaultManager.Tool.Tests.Catalog.CatalogChangeRun;
using static PandaBear.VaultManager.Tool.Tests.Catalog.CatalogFixture;

namespace PandaBear.VaultManager.Tool.Tests.Catalog;

/// <summary>Tests the description of changes that applying a catalog makes.</summary>
public sealed class CatalogChangesTests
{
    /// <summary>Verifies that an unedited catalog gives no changes and no removals.</summary>
    [Fact]
    public void An_unedited_catalog_gives_no_changes_and_no_removals()
    {
        // Arrange
        VaultItem item = Login() with
        {
            Fields = [new CustomField(null, "nameless value", CustomFieldType.Text), new CustomField("Empty", null, CustomFieldType.Text), .. Login().Fields],
            Login = Login().Login! with
            {
                Uris = [new LoginUri("  https://github.com/login  "), new LoginUri(MaskedUri)]
            },
        };

        // Act
        var shared = Apply(item, entry => entry, Notes(NotesDecision.Share));
        var withheld = Apply(item, entry => entry);

        // Assert
        shared.Changes.ShouldBeEmpty();
        shared.Removals.ShouldBeEmpty();
        shared.ChangedItems.ShouldBe(0);
        withheld.Changes.ShouldBeEmpty();
        withheld.Removals.ShouldBeEmpty();
        withheld.ChangedItems.ShouldBe(0);
        var written = withheld.Vault.ShouldNotBeNull().Items.ShouldHaveSingleItem();
        written.Fields.ShouldBe(item.Fields);
        written.Login.ShouldNotBeNull().Uris.ShouldBe(item.Login!.Uris);
    }

    /// <summary>Verifies that an unedited catalog keeps shared notes that consist only of white space.</summary>
    [Fact]
    public void An_unedited_catalog_keeps_shared_notes_that_hold_only_white_space()
    {
        // Arrange
        VaultItem item = Login() with
        {
            Notes = "\n"
        };
        var decisions = MergeDecisions.None with
        {
            Notes = [new ItemNotesDecision(Key, NotesDecision.Share, NotesSha256: ItemNotesDecision.HashOf("\n"))]
        };

        // Act
        var result = Apply(item, entry => entry, decisions);

        // Assert
        result.Changes.ShouldBeEmpty();
        result.Removals.ShouldBeEmpty();
        result.ChangedItems.ShouldBe(0);
        result.Vault.ShouldNotBeNull().Items.ShouldHaveSingleItem().Notes.ShouldBe("\n");
    }

    /// <summary>Verifies that reordered text fields and additional folders are described as changes.</summary>
    [Fact]
    public void Apply_describes_reordered_text_fields_and_additional_folders()
    {
        // Arrange
        VaultItem item = Login() with
        {
            Fields = [new CustomField("Owner", "IT", CustomFieldType.Text), .. Login().Fields.Select(field => field.Name == AdditionalFolders.FieldName ? field with { Value = "Personal\nArchive" } : field)],
        };

        // Act
        var result = Apply(item, entry => entry with { Fields = [.. entry.Fields.Reverse()], AdditionalFolders = [.. entry.AdditionalFolders.Reverse()] });

        // Assert
        result.Changes.ShouldBe([Prefix + "additional folders reordered", Prefix + "text fields reordered"]);
        result.Removals.ShouldBeEmpty();
        result.ChangedItems.ShouldBe(1);
    }

    /// <summary>Verifies that each kind of change to an item produces its description.</summary>
    [Theory]
    [InlineData("rename", "renamed to \"GitHub (work)\"", false)]
    [InlineData("move", "moved from folder \"Work\" to \"Work/Code\"", false)]
    [InlineData("move out", "moved from folder \"Work\" to no folder", false)]
    [InlineData("add additional folder", "additional folder \"Archive\" added", false)]
    [InlineData("remove additional folder", "additional folder \"Personal\" removed", false)]
    [InlineData("favorite", "marked as a favorite", false)]
    [InlineData("add address", "website address \"https://github.com\" added", false)]
    [InlineData("add wide address", "website address \"https://github.com/\" added (offers the login on more sites)", false)]
    [InlineData("remove address", "website address \"https://github.com/login\" removed", true)]
    [InlineData("edit address", "website address \"https://github.com/login\" removed", true)]
    [InlineData("edit address", "website address \"https://github.com/session\" added", false)]
    [InlineData("match by host", "website address \"https://github.com/login\" now matches by Host", false)]
    [InlineData("match by expression", "website address \"https://github.com/login\" now matches by RegularExpression (offers the login on more sites)", false)]
    [InlineData("match by default", "website address \"https://github.com/login\" now matches by the default", false)]
    [InlineData("move masked address", "masked website address 1 moved", false)]
    [InlineData("change field", "text field \"Plan\" changed", true)]
    [InlineData("add field", "text field \"Owner\" added", false)]
    [InlineData("remove field", "text field \"Plan\" removed", true)]
    public void Apply_describes_each_change_of_an_item(string change, string line, bool isRemoval)
    {
        // Arrange
        VaultItem item = change == "match by default"
            ? Login() with
            {
                Login = Login().Login! with
                {
                    Uris = [new LoginUri("https://github.com/login", UriMatch.Exact), new LoginUri(MaskedUri)]
                }
            }
            : Login();
        Func<CatalogItem, CatalogItem> edit = change switch
        {
            "rename" => entry => entry with { Name = "GitHub (work)" },
            "move" => entry => entry with { Folder = "Work/Code" },
            "move out" => entry => entry with { Folder = null },
            "add additional folder" => entry => entry with { AdditionalFolders = ["Personal", "Archive"] },
            "remove additional folder" => entry => entry with { AdditionalFolders = [] },
            "favorite" => entry => entry with { Favorite = true },
            "add address" => entry => entry with { Uris = [.. entry.Uris, new CatalogUri("https://github.com")] },
            "add wide address" => entry => entry with { Uris = [.. entry.Uris, new CatalogUri("https://github.com/", UriMatch.StartsWith)] },
            "remove address" => entry => entry with { Uris = [entry.Uris[1]] },
            "edit address" => entry => entry with { Uris = [new CatalogUri("https://github.com/session"), entry.Uris[1]] },
            "match by host" => entry => entry with { Uris = [entry.Uris[0] with { Match = UriMatch.Host }, entry.Uris[1]] },
            "match by expression" => entry => entry with { Uris = [entry.Uris[0] with { Match = UriMatch.RegularExpression }, entry.Uris[1]] },
            "match by default" => entry => entry with { Uris = [entry.Uris[0] with { Match = null }, entry.Uris[1]] },
            "move masked address" => entry => entry with { Uris = [entry.Uris[1], entry.Uris[0]] },
            "change field" => entry => entry with { Fields = [new CatalogField("Plan", "Enterprise")] },
            "add field" => entry => entry with { Fields = [.. entry.Fields, new CatalogField("Owner", "IT")] },
            _ => entry => entry with { Fields = [] },
        };

        // Act
        var result = Apply(item, edit);

        // Assert
        result.Changes.ShouldContain(Prefix + line);
        result.Removals.Contains(Prefix + line).ShouldBe(isRemoval);
        result.Removals.ShouldAllSatisfy(removal => result.Changes.ShouldContain(removal));
        result.Changes.ShouldAllSatisfy(changeLine =>
        {
            bool namesItemOrFolder = changeLine.StartsWith(Prefix, StringComparison.Ordinal) || changeLine.StartsWith("folder \"", StringComparison.Ordinal);
            namesItemOrFolder.ShouldBeTrue();
            changeLine.ShouldNotContain("Team", StringComparison.Ordinal);
            changeLine.ShouldNotContain("Enterprise", StringComparison.Ordinal);
            changeLine.ShouldNotContain("IT\"", StringComparison.Ordinal);
            changeLine.ShouldNotContain("fixture", StringComparison.Ordinal);
            changeLine.ShouldNotContain("alice", StringComparison.Ordinal);
            changeLine.ShouldNotContain("***", StringComparison.Ordinal);
        });
    }

    /// <summary>Verifies that removing the favorite flag is described.</summary>
    [Fact]
    public void Apply_describes_an_item_that_is_no_longer_a_favorite()
    {
        // Act
        var result = Apply(Login() with
        {
            Favorite = true
        }, entry => entry with { Favorite = false });

        // Assert
        result.Changes.ShouldBe([Prefix + "no longer a favorite"]);
        result.Removals.ShouldBeEmpty();
    }

    /// <summary>Verifies that reordered addresses are described as a reorder, not as additions or removals.</summary>
    [Fact]
    public void Apply_describes_reordered_addresses_without_adding_or_removing_them()
    {
        // Arrange
        VaultItem item = Login() with
        {
            Login = Login().Login! with
            {
                Uris = [new LoginUri("https://github.com/login"), new LoginUri("https://github.com")]
            },
        };

        // Act
        var result = Apply(item, entry => entry with { Uris = [entry.Uris[1], entry.Uris[0]] });

        // Assert
        result.Changes.ShouldBe([Prefix + "website addresses reordered"]);
        result.Removals.ShouldBeEmpty();
    }

    /// <summary>Verifies that text fields are paired by value first and by name second.</summary>
    [Fact]
    public void Apply_pairs_text_fields_by_value_then_by_name()
    {
        // Arrange
        VaultItem item = Login() with
        {
            Fields =
            [
                new CustomField("A", "fixture-a1", CustomFieldType.Text),
                new CustomField("A", "fixture-a2", CustomFieldType.Text),
                new CustomField("B", "fixture-b3", CustomFieldType.Text),
            ],
        };

        // Act
        var result = Apply(item, entry => entry with
        {
            Fields = [new CatalogField("A", "fixture-a2"), new CatalogField("B", "fixture-b4"), new CatalogField("C", "fixture-c5")],
        });

        // Assert
        result.Changes.ShouldBe(
        [
            Prefix + "text field \"A\" removed",
            Prefix + "text field \"B\" changed",
            Prefix + "text field \"C\" added",
        ]);
        result.Removals.ShouldBe([Prefix + "text field \"A\" removed", Prefix + "text field \"B\" changed"]);
    }

    /// <summary>Verifies that notes added to an item that had none are described.</summary>
    [Fact]
    public void Apply_describes_notes_added_to_an_item_without_notes()
    {
        // Act
        var result = Apply(Login() with
        {
            Notes = null
        }, entry => entry with { Notes = new CatalogNotes(NotesDecision.Share, "fixture added notes") });

        // Assert
        result.Changes.ShouldBe([Prefix + "notes added"]);
        result.Removals.ShouldBeEmpty();
    }

    /// <summary>Verifies that edited shared notes are described.</summary>
    [Theory]
    [InlineData("fixture edited notes", "notes edited", false)]
    [InlineData(" ", "notes emptied", true)]
    public void Apply_describes_edited_shared_notes(string text, string line, bool isRemoval)
    {
        // Act
        var result = Apply(Login(), entry => entry with { Notes = new CatalogNotes(NotesDecision.Share, text) }, Notes(NotesDecision.Share));

        // Assert
        result.Changes.ShouldBe([Prefix + line]);
        result.Removals.Contains(Prefix + line).ShouldBe(isRemoval);
    }

    /// <summary>Verifies that notes moved into a hidden field are described.</summary>
    [Fact]
    public void Apply_describes_notes_moved_into_a_hidden_field()
    {
        // Act
        var result = Apply(Login(), entry => entry, Notes(NotesDecision.Hidden));

        // Assert
        result.Changes.ShouldBe([Prefix + "notes moved into a hidden field named \"Notes\""]);
        result.Removals.ShouldBeEmpty();
    }

    /// <summary>Verifies that added and removed folders of the vault are described.</summary>
    [Fact]
    public void Apply_describes_added_and_removed_folders_of_the_vault()
    {
        // Act
        var added = Apply(Login(), entry => entry with { Folder = "Work/Code" }, folders: ["Archive", "Empty"]);
        var removed = Apply(Login(), entry => entry, folders: []);

        // Assert
        added.Changes.ShouldBe(
        [
            Prefix + "moved from folder \"Work\" to \"Work/Code\"",
            "folder \"Empty\" added",
            "folder \"Work/Code\" added",
        ]);
        added.Removals.ShouldBeEmpty();
        removed.Changes.ShouldBe(["folder \"Archive\" removed"]);
        removed.Removals.ShouldBe(["folder \"Archive\" removed"]);
    }
}
