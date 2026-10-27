using System.Globalization;
using PandaBear.VaultManager.Tool.Formats.Bitwarden;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Tests.Formats.Bitwarden;

/// <summary>Tests the reader for Bitwarden JSON exports.</summary>
public sealed class BitwardenJsonReaderTests
{
    [Fact]
    public void Read_maps_login_fields_folder_history_and_passkeys()
    {
        // Act
        var items = Fixture.ReadBitwardenFull().Items;

        // Assert
        var login = items.ShouldHaveSingleItem(item => item.Name == "Example login");
        login.Type.ShouldBe(VaultItemType.Login);
        login.FolderPath.ShouldBe("Work");
        login.Favorite.ShouldBeTrue();
        login.Reprompt.ShouldBeTrue();
        var data = login.Login.ShouldNotBeNull();
        data.Username.ShouldBe("alice");
        data.Password.ShouldBe("fixture-password-1");
        data.Totp.ShouldStartWith("otpauth://totp/", StringComparison.Ordinal);
        data.Uris.Select(uri => uri.Value).ShouldBe(["https://example.com/login", "https://accounts.example.com"]);
        data.Uris[1].Match.ShouldBe(UriMatch.Exact);
        data.Fido2Credentials.ShouldHaveSingleItem().RpId.ShouldBe("example.com");
        login.PasswordHistory.Select(entry => entry.Password).ShouldBe(["newer-password", "older-password"]);
        login.Fields[0].Type.ShouldBe(CustomFieldType.Hidden);
        login.Origin.ShouldBe(new ItemOrigin("bitwarden", "full.json", "login-1", null));
        login.LastModified.ShouldBe(DateTimeOffset.Parse("2026-03-04T05:06:07Z", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Read_maps_note_card_identity_and_ssh_key_items()
    {
        // Act
        var export = Fixture.ReadBitwardenFull();

        // Assert
        export.Items.ShouldHaveSingleItem(item => item.Name == "Plain note").Notes.ShouldBe("note body");
        export.Items.ShouldHaveSingleItem(item => item.Type == VaultItemType.Card).Card.ShouldNotBeNull().Number.ShouldBe("4111111111111111");
        export.Items.ShouldHaveSingleItem(item => item.Type == VaultItemType.Identity).Identity.ShouldNotBeNull().PassportNumber.ShouldBe("X1234567");
        var ssh = export.Items.ShouldHaveSingleItem(item => item.Type == VaultItemType.SshKey);
        ssh.FolderPath.ShouldBe("Work/Servers");
        ssh.SshKey.ShouldNotBeNull().KeyFingerprint.ShouldBe("SHA256:fixture");
    }

    [Fact]
    public void Read_skips_deleted_items()
    {
        // Act
        var items = Fixture.ReadBitwardenFull().Items;

        // Assert
        items.ShouldNotContain(item => item.Name == "Deleted note");
    }

    [Fact]
    public void Read_unknown_item_type_becomes_secure_note_with_hidden_fields_and_note()
    {
        // Act
        var items = Fixture.ReadBitwardenFull().Items;

        // Assert
        var bank = items.ShouldHaveSingleItem(item => item.Name == "Fixture bank account");
        bank.Type.ShouldBe(VaultItemType.SecureNote);
        bank.Fields.ShouldContain(new CustomField("iban", "DE89370400440532013000", CustomFieldType.Hidden));
        bank.Fields.ShouldContain(new CustomField("bankName", "Example Bank", CustomFieldType.Hidden));
        bank.ConversionNotes.ShouldContain(note => note.Contains("type 6", StringComparison.Ordinal));
    }

    [Fact]
    public void Read_account_restricted_export_throws_with_explanation()
    {
        // Arrange
        const string json = """{"encrypted":true,"encKeyValidation_DO_NOT_EDIT":"2.a|b|c","folders":[],"items":[]}""";
        var action = () => BitwardenJsonReader.Read(json, "restricted.json");

        // Act
        var exception = action.ShouldThrow<InvalidDataException>();

        // Assert
        exception.Message.ShouldContain("account-restricted", StringComparison.Ordinal);
    }

    [Fact]
    public void Read_organization_export_uses_first_collection_as_folder()
    {
        // Arrange
        const string json = """
            {"encrypted":false,"collections":[{"id":"c1","name":"Shared"},{"id":"c2","name":"Other"}],
             "items":[{"id":"i","type":2,"name":"n","secureNote":{"type":0},"collectionIds":["c1","c2"]}]}
            """;

        // Act
        var items = BitwardenJsonReader.Read(json, "org.json").Items;

        // Assert
        var item = items.ShouldHaveSingleItem();
        item.FolderPath.ShouldBe("Shared");
        item.Fields.ShouldBe([new CustomField("Additional folders", "Other", CustomFieldType.Text)]);
        item.ConversionNotes.ShouldHaveSingleItem();
    }

    [Fact]
    public void Read_keeps_every_named_folder_including_folders_that_hold_no_item()
    {
        // Arrange
        const string json = """
            {"encrypted":false,"folders":[{"id":"f1","name":"Work"},{"id":"f2","name":"Empty"},{"id":"f3","name":" "}],
             "items":[{"id":"i","type":2,"name":"n","secureNote":{"type":0},"folderId":"f1"}]}
            """;

        // Act
        var export = BitwardenJsonReader.Read(json, "personal.json");

        // Assert
        export.Folders.ShouldBe(["Work", "Empty"]);
    }

    [Fact]
    public void Read_export_with_folders_and_collections_keeps_both()
    {
        // Arrange
        const string json = """
            {"encrypted":false,"folders":[{"id":"f1","name":"Personal"}],"collections":[{"id":"c1","name":"Team"}],
             "items":[{"id":"a","type":2,"name":"a","secureNote":{"type":0},"folderId":"f1","collectionIds":["c1"]},
                      {"id":"b","type":2,"name":"b","secureNote":{"type":0},"collectionIds":["c1"]}]}
            """;

        // Act
        var export = BitwardenJsonReader.Read(json, "both.json");

        // Assert
        export.Folders.ShouldBe(["Personal", "Team"]);
        export.Items[0].FolderPath.ShouldBe("Personal");
        export.Items[0].Fields.ShouldBe([new CustomField("Additional folders", "Team", CustomFieldType.Text)]);
        export.Items[1].FolderPath.ShouldBe("Team");
        export.Items[1].Fields.ShouldBeEmpty();
    }

    [Fact]
    public void Read_notes_a_folder_ID_that_names_no_folder()
    {
        // Arrange
        const string json = """
            {"encrypted":false,"folders":[],"items":[{"id":"i","type":2,"name":"n","secureNote":{"type":0},"folderId":"missing"}]}
            """;

        // Act
        var items = BitwardenJsonReader.Read(json, "personal.json").Items;

        // Assert
        var item = items.ShouldHaveSingleItem();
        item.FolderPath.ShouldBeNull();
        item.ConversionNotes.ShouldHaveSingleItem().ShouldContain("folder", StringComparison.Ordinal);
    }

    [Fact]
    public void Read_invalid_JSON_throws_invalid_data()
    {
        // Arrange
        var action = () => BitwardenJsonReader.Read("{ not json", "bad.json");

        // Act, Assert
        action.ShouldThrow<InvalidDataException>();
    }

    [Theory]
    [InlineData("""{"encrypted":false,"folders":[],"items":[{"id":"i","type":2,"name":"n"},null]}""", "Item 2 of the Bitwarden export is null.")]
    [InlineData("""{"encrypted":false,"folders":[{"id":"f","name":"Work"},null],"items":[]}""", "Folder 2 of the Bitwarden export is null.")]
    [InlineData("""{"encrypted":false,"collections":[null],"items":[]}""", "Collection 1 of the Bitwarden export is null.")]
    [InlineData("""{"encrypted":false,"folders":[],"items":[{"id":"i","type":2,"name":"n","fields":[null]}]}""", "Item 1 of the Bitwarden export has a null entry in its custom fields.")]
    [InlineData("""{"encrypted":false,"folders":[],"items":[{"id":"i","type":1,"name":"n","passwordHistory":[null]}]}""", "Item 1 of the Bitwarden export has a null entry in its password history.")]
    [InlineData("""{"encrypted":false,"collections":[],"items":[{"id":"i","type":2,"name":"n","collectionIds":[null]}]}""", "Item 1 of the Bitwarden export has a null entry in its collection IDs.")]
    [InlineData("""{"encrypted":false,"folders":[],"items":[{"id":"i","type":1,"name":"n","login":{"uris":[null]}}]}""", "Item 1 of the Bitwarden export has a null entry in its URIs.")]
    [InlineData("""{"encrypted":false,"folders":[],"items":[{"id":"i","type":1,"name":"n","login":{"fido2Credentials":[null]}}]}""", "Item 1 of the Bitwarden export has a null entry in its passkeys.")]
    public void Read_export_with_a_null_entry_throws_invalid_data_that_names_the_entry_by_position(string json, string message)
    {
        // Arrange
        var action = () => BitwardenJsonReader.Read(json, "null-entry.json");

        // Act
        var exception = action.ShouldThrow<InvalidDataException>();

        // Assert
        exception.Message.ShouldBe(message);
    }

    [Theory]
    [InlineData("folders", "Folders 1 and 3 of the Bitwarden export have the same ID.")]
    [InlineData("collections", "Collections 1 and 3 of the Bitwarden export have the same ID.")]
    public void Read_export_whose_folders_repeat_an_ID_throws_invalid_data_that_names_them_by_position(string list, string message)
    {
        // Arrange
        string json = "{\"encrypted\":false,\"" + list + "\":[{\"id\":\"f1\",\"name\":\"Work\"},{\"id\":\"f2\",\"name\":\"Home\"},{\"id\":\"f1\",\"name\":\"Bank\"}],"
            + "\"items\":[{\"id\":\"i\",\"type\":2,\"name\":\"n\",\"folderId\":\"f1\"}]}";
        var action = () => BitwardenJsonReader.Read(json, "duplicate-folders.json");

        // Act
        var exception = action.ShouldThrow<InvalidDataException>();

        // Assert
        exception.Message.ShouldBe(message);
    }

    [Fact]
    public void Read_ignores_null_entries_of_deleted_items_it_does_not_read()
    {
        // Arrange
        const string json = """
            {"encrypted":false,"folders":[],"items":[
              {"id":"deleted","type":1,"name":"Old","deletedDate":"2026-01-01T00:00:00Z","fields":[null],"login":{"uris":[null]}},
              {"id":"kept","type":2,"name":"Kept"}]}
            """;

        // Act
        var items = BitwardenJsonReader.Read(json, "deleted.json").Items;

        // Assert
        var item = items.ShouldHaveSingleItem();
        item.Name.ShouldBe("Kept");
    }
}
