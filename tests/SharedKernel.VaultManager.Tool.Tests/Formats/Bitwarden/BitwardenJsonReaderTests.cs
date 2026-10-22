using System.Globalization;
using PandaBear.VaultManager.Tool.Formats.Bitwarden;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Tests.Formats.Bitwarden;

public sealed class BitwardenJsonReaderTests
{
    [Fact]
    public void Read_maps_login_fields_folder_history_and_passkeys()
    {
        var login = Fixture.ReadBitwardenFull().Items.ShouldHaveSingleItem(item => item.Name == "Example login");

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
        var export = Fixture.ReadBitwardenFull();

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
        Fixture.ReadBitwardenFull().Items.ShouldNotContain(item => item.Name == "Deleted note");
    }

    [Fact]
    public void Read_unknown_item_type_becomes_secure_note_with_hidden_fields_and_note()
    {
        var bank = Fixture.ReadBitwardenFull().Items.ShouldHaveSingleItem(item => item.Name == "Fixture bank account");

        bank.Type.ShouldBe(VaultItemType.SecureNote);
        bank.Fields.ShouldContain(new CustomField("iban", "DE89370400440532013000", CustomFieldType.Hidden));
        bank.Fields.ShouldContain(new CustomField("bankName", "Example Bank", CustomFieldType.Hidden));
        bank.ConversionNotes.ShouldContain(note => note.Contains("type 6", StringComparison.Ordinal));
    }

    [Fact]
    public void Read_account_restricted_export_throws_with_explanation()
    {
        const string json = """{"encrypted":true,"encKeyValidation_DO_NOT_EDIT":"2.a|b|c","folders":[],"items":[]}""";

        var action = () => BitwardenJsonReader.Read(json, "restricted.json");

        var exception = action.ShouldThrow<InvalidDataException>();

        exception.Message.ShouldContain("account-restricted", StringComparison.Ordinal);
    }

    [Fact]
    public void Read_organization_export_uses_first_collection_as_folder()
    {
        const string json = """
            {"encrypted":false,"collections":[{"id":"c1","name":"Shared"},{"id":"c2","name":"Other"}],
             "items":[{"id":"i","type":2,"name":"n","secureNote":{"type":0},"collectionIds":["c1","c2"]}]}
            """;

        var item = BitwardenJsonReader.Read(json, "org.json").Items.ShouldHaveSingleItem();

        item.FolderPath.ShouldBe("Shared");
        item.ConversionNotes.ShouldHaveSingleItem();
    }

    [Fact]
    public void Read_invalid_JSON_throws_invalid_data()
    {
        var action = () => BitwardenJsonReader.Read("{ not json", "bad.json");

        action.ShouldThrow<InvalidDataException>();
    }
}
