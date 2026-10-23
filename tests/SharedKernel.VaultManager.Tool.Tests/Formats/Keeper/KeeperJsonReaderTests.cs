using System.Globalization;
using System.Security.Cryptography;
using PandaBear.VaultManager.Tool.Formats.Keeper;
using PandaBear.VaultManager.Tool.Model;
using static PandaBear.VaultManager.Tool.Tests.Formats.Keeper.KeeperTestData;

namespace PandaBear.VaultManager.Tool.Tests.Formats.Keeper;

public sealed class KeeperJsonReaderTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("vault-manager-keeper-").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public void Read_keeps_shared_folders_and_record_folders_as_declared_folders()
    {
        const string json = """
            {"shared_folders":[{"uid":"s1","path":"Team\\Empty"}],
             "records":[{"uid":"r","title":"t","folders":[{"shared_folder":"Team","folder":"Web"},{"folder":"Personal"}]}]}
            """;

        KeeperJsonReader.Read(json, "keeper.json").Export.Folders.ShouldBe(["Team/Empty", "Team/Web", "Personal"]);
    }

    [Fact]
    public void Read_replaces_a_slash_in_a_folder_name_and_notes_the_original_name()
    {
        const string json = """{"records":[{"uid":"r","title":"t","folders":[{"folder":"Bills\\A/B"}]}]}""";

        var item = KeeperJsonReader.Read(json, "keeper.json").Export.Items.ShouldHaveSingleItem();

        item.FolderPath.ShouldBe("Bills/A-B");
        item.ConversionNotes.ShouldContain(note => note.Contains("\"A/B\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Read_commander_login_maps_login_totps_uris_fields_folders_and_timestamp()
    {
        var item = ReadCommander().Items.ShouldHaveSingleItem(item => item.Name == "Example login");

        item.Type.ShouldBe(VaultItemType.Login);
        item.Notes.ShouldBe("login notes");
        item.FolderPath.ShouldBe("Team/Shared/Web/Sites");
        item.Origin.ShouldBe(new ItemOrigin("keeper", "commander.json", "rec-login", DateTimeOffset.Parse("2026-01-01T00:00:00Z", CultureInfo.InvariantCulture)));
        var login = item.Login.ShouldNotBeNull();
        login.Username.ShouldBe("alice");
        login.Password.ShouldBe("fixture-password-1");
        login.Totp.ShouldBe("otpauth://totp/Example:alice?secret=JBSWY3DPEHPK3PXP&issuer=Example");
        login.Uris.Select(uri => uri.Value).ShouldBe(["https://example.com/login", "https://accounts.example.com"]);
        item.Fields.ShouldBe(
        [
            Field("TOTP", "otpauth://totp/Example:backup?secret=KRSXG5CTMVRXEZLU&issuer=Example", CustomFieldType.Hidden),
            Field("Plain label", "plain value"),
            Field("Nickname", "ali"),
            Field("Active", "true", CustomFieldType.Boolean),
            Field("First pet?", "fixture-answer-1", CustomFieldType.Hidden),
            Field("Birth town?", "fixture-answer-2", CustomFieldType.Hidden),
            Field("Additional folders", "Personal"),
        ]);
    }

    [Fact]
    public void Read_commander_login_records_unrepresentable_data_in_conversion_notes()
    {
        var item = ReadCommander().Items.ShouldHaveSingleItem(item => item.Name == "Example login");

        item.ConversionNotes.ShouldContain(note => note.Contains("other folder", StringComparison.Ordinal));
        item.ConversionNotes.ShouldContain(note => note.Contains("permissions", StringComparison.Ordinal));
        item.ConversionNotes.ShouldContain(note => note.Contains("references", StringComparison.Ordinal));
        item.ConversionNotes.ShouldContain(note => note.Contains("schema", StringComparison.Ordinal));
    }

    [Fact]
    public void Read_commander_export_warns_about_shared_folder_permissions_without_values()
    {
        var result = KeeperJsonReader.Read(Fixture.Read("keeper/commander.json"), "commander.json");

        result.Warnings.ShouldContain(warning => warning.Contains("permissions", StringComparison.Ordinal));
        result.Warnings.ShouldAllSatisfy(warning => warning.ShouldNotContain("member@example.com", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Example login", "Login")]
    [InlineData("Checking account", "SecureNote")]
    [InlineData("Home address", "Identity")]
    [InlineData("Example card", "Card")]
    [InlineData("Birth certificate", "Identity")]
    [InlineData("Bob contact", "Identity")]
    [InlineData("Driver license", "Identity")]
    [InlineData("Secret note", "SecureNote")]
    [InlineData("Scanned file", "SecureNote")]
    [InlineData("Health plan", "Login")]
    [InlineData("Gym membership", "SecureNote")]
    [InlineData("Passport", "Identity")]
    [InlineData("Holiday photo", "SecureNote")]
    [InlineData("Prod server", "Login")]
    [InlineData("Editor license", "SecureNote")]
    [InlineData("Social security", "Identity")]
    [InlineData("General record", "Login")]
    [InlineData("Key without private part", "SecureNote")]
    [InlineData("Main database", "Login")]
    [InlineData("Home wifi", "Login")]
    [InlineData("PAM machine", "Login")]
    [InlineData("Custom API record", "SecureNote")]
    public void Read_maps_every_keeper_record_type(string title, string expected)
    {
        var item = ReadCommander().Items.ShouldHaveSingleItem(item => item.Name == title);

        item.Type.ShouldBe(Enum.Parse<VaultItemType>(expected));
    }

    [Fact]
    public void Read_bank_card_fills_card_data_and_keeps_pin_and_login_as_fields()
    {
        var item = ReadCommander().Items.ShouldHaveSingleItem(item => item.Name == "Example card");

        var card = item.Card.ShouldNotBeNull();
        card.Number.ShouldBe("4111111111111111");
        card.Code.ShouldBe("123");
        card.ExpMonth.ShouldBe("03");
        card.ExpYear.ShouldBe("2030");
        card.CardholderName.ShouldBe("Alice Example");
        item.Login.ShouldBeNull();
        item.Fields.ShouldBe([Field("PIN", "4321", CustomFieldType.Hidden), Field("Username", "card-user")]);
    }

    [Fact]
    public void Read_bank_account_keeps_account_numbers_hidden_in_bitwarden_format()
    {
        var item = ReadCommander().Items.ShouldHaveSingleItem(item => item.Name == "Checking account");

        item.Fields.ShouldBe(
        [
            Field("bankAccount", "Type: Checking, Account Number: 12345678, Routing Number: 021000021", CustomFieldType.Hidden),
            Field("name", "Alice Example"),
        ]);
    }

    [Fact]
    public void Read_identity_records_fill_identity_slots()
    {
        var items = ReadCommander().Items;

        var address = items.ShouldHaveSingleItem(item => item.Name == "Home address").Identity.ShouldNotBeNull();
        address.ShouldBe(new IdentityData
        {
            Address1 = "1 Main St",
            Address2 = "Apt 2",
            City = "Springfield",
            State = "IL",
            PostalCode = "62701",
            Country = "US",
        });
        var contact = items.ShouldHaveSingleItem(item => item.Name == "Bob contact");
        contact.Identity.ShouldBe(new IdentityData
        {
            FirstName = "Bob",
            LastName = "Builder",
            Company = "Example Corp",
            Email = "bob@example.com",
            Phone = "(US) 555-0100 ext. 12 (Work)",
        });
        contact.Fields.ShouldBe([Field("phone", "555-0199 (Mobile)")]);
        items.ShouldHaveSingleItem(item => item.Name == "Passport").Identity.ShouldNotBeNull().PassportNumber.ShouldBe("X1234567");
        items.ShouldHaveSingleItem(item => item.Name == "Social security").Identity.ShouldNotBeNull().Ssn.ShouldBe("000-00-0000");
        var license = items.ShouldHaveSingleItem(item => item.Name == "Driver license");
        license.Identity.ShouldBe(new IdentityData { FirstName = "Alice", LastName = "Example", LicenseNumber = "D1234567" });
        license.Fields.ShouldBe([Field("birthDate", "1990-01-15"), Field("expirationDate", "2030-01-01")]);
        var birth = items.ShouldHaveSingleItem(item => item.Name == "Birth certificate");
        birth.Identity.ShouldBe(new IdentityData { FirstName = "Alice", MiddleName = "Q", LastName = "Example" });
        birth.Fields.ShouldBe([Field("birthDate", "1990-01-15")]);
    }

    [Fact]
    public void Read_maps_hidden_text_and_date_fields_of_other_record_types()
    {
        var items = ReadCommander().Items;

        items.ShouldHaveSingleItem(item => item.Name == "Secret note").Fields
            .ShouldBe([Field("note", "secured note text"), Field("date", "2025-06-30")]);
        items.ShouldHaveSingleItem(item => item.Name == "Editor license").Fields
            .ShouldBe([Field("licenseNumber", "LIC-0001", CustomFieldType.Hidden), Field("dateActive", "2025-06-30")]);
        items.ShouldHaveSingleItem(item => item.Name == "Health plan").Fields
            .ShouldBe([Field("accountNumber", "H-998877", CustomFieldType.Hidden)]);
        items.ShouldHaveSingleItem(item => item.Name == "Prod server").Fields
            .ShouldBe([Field("Hostname", "server.example.com"), Field("Port", "2222")]);
        items.ShouldHaveSingleItem(item => item.Name == "Main database").Fields
            .ShouldBe([Field("type", "postgres"), Field("Hostname", "db.example.com"), Field("Port", "5432")]);
        items.ShouldHaveSingleItem(item => item.Name == "PAM machine").Fields
            .ShouldBe([Field("pamHostname", """{"hostName":"pam.example.com","port":"22"}"""), Field("pamSettings", """{"connection":{"protocol":"ssh"}}""")]);
        items.ShouldHaveSingleItem(item => item.Name == "Custom API record").Fields
            .ShouldBe([Field("API key", "fixture-api-key", CustomFieldType.Hidden), Field("Comment", "first\nsecond")]);
        items.ShouldHaveSingleItem(item => item.Name == "Scanned file").Attachments
            .ShouldBe([new AttachmentRef("scan.pdf", null, null)]);
    }

    [Fact]
    public void Read_ssh_key_record_without_private_key_becomes_note_with_passphrase_and_public_key()
    {
        var item = ReadCommander().Items.ShouldHaveSingleItem(item => item.Name == "Key without private part");

        item.SshKey.ShouldBeNull();
        item.Fields.ShouldBe(
        [
            Field("Username", "deploy"),
            Field("Passphrase", "fixture-passphrase", CustomFieldType.Hidden),
            Field("Public key", "ssh-ed25519 AAAAfixture deploy@example"),
        ]);
    }

    [Fact]
    public void Read_ssh_key_record_with_private_key_becomes_ssh_key_item()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string pem = key.ExportPkcs8PrivateKeyPem();

        var item = KeeperJsonReader.Read(SshKeyRecord(pem, "ecdsa-sha2-nistp256 AAAAfixture"), "ssh.json").Export.Items.ShouldHaveSingleItem();

        item.Type.ShouldBe(VaultItemType.SshKey);
        item.SshKey.ShouldBe(new SshKeyData { PrivateKey = pem, PublicKey = "ecdsa-sha2-nistp256 AAAAfixture", KeyFingerprint = null });
        item.Fields.ShouldBe(
        [
            Field("Username", "deploy"),
            Field("Password", "fixture-passphrase", CustomFieldType.Hidden),
            Field("Hostname", "git.example.com"),
            Field("Port", "22"),
        ]);
    }

    [Fact]
    public void Read_passkey_becomes_fido2_credential_whose_key_signs_for_the_original_public_key()
    {
        using var original = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ECParameters parameters = original.ExportParameters(includePrivateParameters: true);
        byte[] credentialId = RandomNumberGenerator.GetBytes(16);

        var item = KeeperJsonReader.Read(LoginWithPasskey(PasskeyJwk(parameters), Base64Url(credentialId)), "passkey.json").Export.Items.ShouldHaveSingleItem();

        var credential = item.Login.ShouldNotBeNull().Fido2Credentials.ShouldHaveSingleItem();
        credential.CredentialId.ShouldBe("b64." + Base64Url(credentialId));
        credential.RpId.ShouldBe("passkey.example.com");
        credential.UserName.ShouldBe("alice@example.com");
        credential.UserHandle.ShouldBe("dXNlci1oYW5kbGU");
        credential.Counter.ShouldBe("7");
        credential.Discoverable.ShouldBe("true");
        credential.CreationDate.ShouldBe(DateTimeOffset.FromUnixTimeMilliseconds(1751241600000));
        credential.KeyValue.ShouldNotContain("=", StringComparison.Ordinal);
        item.Fields.ShouldBeEmpty();

        using var converted = ECDsa.Create();
        converted.ImportPkcs8PrivateKey(FromBase64Url(credential.KeyValue), out _);
        byte[] data = "challenge"u8.ToArray();
        byte[] signature = converted.SignData(data, HashAlgorithmName.SHA256);
        original.VerifyData(data, signature, HashAlgorithmName.SHA256).ShouldBeTrue();
    }

    [Fact]
    public void Read_invalid_passkey_is_kept_as_hidden_field_and_warns_without_values()
    {
        using var original = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var jwk = PasskeyJwk(original.ExportParameters(includePrivateParameters: true));
        string privateScalar = jwk["d"]!.GetValue<string>();
        jwk["d"] = "not-a-scalar";

        var result = KeeperJsonReader.Read(LoginWithPasskey(jwk, "AAAA"), "passkey.json");

        var item = result.Export.Items.ShouldHaveSingleItem();
        item.Login.ShouldNotBeNull().Fido2Credentials.ShouldBeEmpty();
        var field = item.Fields.ShouldHaveSingleItem();
        field.Name.ShouldBe("passkey");
        field.Type.ShouldBe(CustomFieldType.Hidden);
        var warning = result.Warnings.ShouldHaveSingleItem();
        warning.ShouldContain("Passkey login", StringComparison.Ordinal);
        warning.ShouldNotContain("not-a-scalar", StringComparison.Ordinal);
        warning.ShouldNotContain(privateScalar, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_web_export_parses_numbered_keys_legacy_totp_escaped_folders_and_has_no_timestamp()
    {
        var export = KeeperJsonReader.Read(Fixture.Read("keeper/web.json"), "web.json").Export;

        var item = export.Items.ShouldHaveSingleItem(item => item.Name == "Web login");
        item.Origin.ShouldBe(new ItemOrigin("keeper", "web.json", "1", null));
        item.FolderPath.ShouldBe("Shared\\Literal/Nested-Name");
        var login = item.Login.ShouldNotBeNull();
        login.Totp.ShouldBe("otpauth://totp/Web:web-user?secret=JBSWY3DPEHPK3PXP&issuer=Web");
        login.Uris.Select(uri => uri.Value).ShouldBe(["https://web.example.com", "https://account.web.example.com"]);
        item.Fields.ShouldBe(
        [
            Field("name", "Web User"),
            Field("TOTP", "otpauth://totp/Legacy:web-user?secret=KRSXG5CTMVRXEZLU&issuer=Legacy", CustomFieldType.Hidden),
        ]);
        var untitled = export.Items.ShouldHaveSingleItem(item => item.Notes == "untitled note");
        untitled.Name.ShouldBe("--");
        untitled.Type.ShouldBe(VaultItemType.SecureNote);
    }

    [Fact]
    public void Read_accepts_bare_record_array()
    {
        var item = KeeperJsonReader.Read("""[{"uid":"a","title":"Bare","login":"u"}]""", "bare.json").Export.Items.ShouldHaveSingleItem();

        item.Name.ShouldBe("Bare");
        item.Type.ShouldBe(VaultItemType.Login);
    }

    [Fact]
    public void Read_invalid_JSON_throws_invalid_data()
    {
        var action = () => KeeperJsonReader.Read("{ not json", "bad.json");

        action.ShouldThrow<InvalidDataException>();
    }

    [Fact]
    public void ReadZip_attaches_files_by_file_uid_and_warns_about_missing_files()
    {
        string path = Path.Combine(directory, "keeper.zip");
        File.WriteAllBytes(path, TestZip.Create(("export.json", TestZip.Utf8(CommanderZipExport())), ("files/file-1", "%PDF"u8.ToArray())));

        var result = KeeperJsonReader.ReadZip(path);

        var item = result.Export.Items.ShouldHaveSingleItem();
        item.Origin.SourceFile.ShouldBe("keeper.zip");
        item.Attachments.ShouldBe([new AttachmentRef("scan.pdf", 4, "files/file-1"), new AttachmentRef("missing.txt", null, null)]);
        result.Warnings.ShouldHaveSingleItem().ShouldContain("missing.txt", StringComparison.Ordinal);
    }

    [Fact]
    public void ReadZip_warns_about_files_no_record_lists_and_about_other_entries()
    {
        string path = Path.Combine(directory, "keeper.zip");
        File.WriteAllBytes(path, TestZip.Create(
            ("export.json", TestZip.Utf8(CommanderZipExport())),
            ("files/file-1", "%PDF"u8.ToArray()),
            ("files/fu-orphan", TestZip.Utf8("fixture orphan")),
            ("other/stray.txt", TestZip.Utf8("fixture stray"))));

        var result = KeeperJsonReader.ReadZip(path);

        result.Warnings.ShouldBe(
        [
            "\"Record with files\": attachment \"missing.txt\" is listed but not included in the zip.",
            "Zip entry \"files/fu-orphan\" belongs to no record; upload it by hand to the item it belongs to after the import.",
            "Zip entry \"other/stray.txt\" is not a Keeper attachment and was ignored.",
        ]);
    }

    [Fact]
    public void ReadZip_without_export_json_throws_invalid_data()
    {
        string path = Path.Combine(directory, "other.zip");
        File.WriteAllBytes(path, TestZip.Create(("data.json", TestZip.Utf8("{}"))));

        var action = () => KeeperJsonReader.ReadZip(path);

        action.ShouldThrow<InvalidDataException>();
    }

    [Fact]
    public void Read_warnings_and_errors_never_contain_values()
    {
        var result = KeeperJsonReader.Read(Fixture.Read("keeper/commander.json"), "commander.json");
        var action = () => KeeperJsonReader.Read("""{"records":"fixture-password-1"}""", "bad.json");

        var exception = action.ShouldThrow<InvalidDataException>();

        exception.Message.ShouldNotContain("fixture-password-1", StringComparison.Ordinal);
        result.Warnings.ShouldAllSatisfy(warning => warning.ShouldNotContain("fixture", StringComparison.Ordinal));
        result.Export.Items.SelectMany(item => item.ConversionNotes)
            .ShouldAllSatisfy(note => note.ShouldNotContain("fixture", StringComparison.Ordinal));
    }
}
