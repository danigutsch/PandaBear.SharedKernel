using System.Globalization;
using System.Security.Cryptography;
using PandaBear.VaultManager.Tool.Formats.Keeper;
using PandaBear.VaultManager.Tool.Model;
using static PandaBear.VaultManager.Tool.Tests.Formats.Keeper.KeeperTestData;

namespace PandaBear.VaultManager.Tool.Tests.Formats.Keeper;

/// <summary>Tests the reader for Keeper JSON and zip exports.</summary>
public sealed class KeeperJsonReaderTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("vault-manager-keeper-").FullName;

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(directory, recursive: true);

    /// <summary>A shared folder with a backslash path and records filed under shared and personal folders give the declared folders "Team/Empty", "Team/Web", and "Personal".</summary>
    [Fact]
    public void Read_keeps_shared_folders_and_record_folders_as_declared_folders()
    {
        // Arrange
        const string json = """
            {"shared_folders":[{"uid":"s1","path":"Team\\Empty"}],
             "records":[{"uid":"r","title":"t","folders":[{"shared_folder":"Team","folder":"Web"},{"folder":"Personal"}]}]}
            """;

        // Act
        var export = KeeperJsonReader.Read(json, "keeper.json").Export;

        // Assert
        export.Folders.ShouldBe(["Team/Empty", "Team/Web", "Personal"]);
    }

    /// <summary>The folder name "A/B" in "Bills\A/B" becomes "A-B" in the folder path "Bills/A-B" and a conversion note quotes the original name.</summary>
    [Fact]
    public void Read_replaces_a_slash_in_a_folder_name_and_notes_the_original_name()
    {
        // Arrange
        const string json = """{"records":[{"uid":"r","title":"t","folders":[{"folder":"Bills\\A/B"}]}]}""";

        // Act
        var items = KeeperJsonReader.Read(json, "keeper.json").Export.Items;

        // Assert
        var item = items.ShouldHaveSingleItem();
        item.FolderPath.ShouldBe("Bills/A-B");
        item.ConversionNotes.ShouldContain(note => note.Contains("\"A/B\"", StringComparison.Ordinal));
    }

    /// <summary>The "Example login" record of commander.json maps its notes, nested folder, origin with timestamp, credentials, TOTP, URIs, and typed custom fields including the extra folder.</summary>
    [Fact]
    public void Read_commander_login_maps_login_totps_uris_fields_folders_and_timestamp()
    {
        // Act
        var items = ReadCommander().Items;

        // Assert
        var item = items.ShouldHaveSingleItem(item => item.Name == "Example login");
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

    /// <summary>The "Example login" record gets conversion notes that mention its other folder, permissions, references, and schema.</summary>
    [Fact]
    public void Read_commander_login_records_unrepresentable_data_in_conversion_notes()
    {
        // Act
        var items = ReadCommander().Items;

        // Assert
        var item = items.ShouldHaveSingleItem(item => item.Name == "Example login");
        item.ConversionNotes.ShouldContain(note => note.Contains("other folder", StringComparison.Ordinal));
        item.ConversionNotes.ShouldContain(note => note.Contains("permissions", StringComparison.Ordinal));
        item.ConversionNotes.ShouldContain(note => note.Contains("references", StringComparison.Ordinal));
        item.ConversionNotes.ShouldContain(note => note.Contains("schema", StringComparison.Ordinal));
    }

    /// <summary>Reading commander.json warns about shared folder permissions without repeating the member's email address.</summary>
    [Fact]
    public void Read_commander_export_warns_about_shared_folder_permissions_without_values()
    {
        // Act
        var result = KeeperJsonReader.Read(Fixture.Read("keeper/commander.json"), "commander.json");

        // Assert
        result.Warnings.ShouldContain(warning => warning.Contains("permissions", StringComparison.Ordinal));
        result.Warnings.ShouldAllSatisfy(warning => warning.ShouldNotContain("member@example.com", StringComparison.Ordinal));
    }

    /// <summary>Each titled record type in commander.json becomes the expected vault item type: login, secure note, identity, or card.</summary>
    /// <param name="title">The title of the record in commander.json.</param>
    /// <param name="expected">The name of the expected item type.</param>
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
        // Act
        var items = ReadCommander().Items;

        // Assert
        var item = items.ShouldHaveSingleItem(item => item.Name == title);
        item.Type.ShouldBe(Enum.Parse<VaultItemType>(expected));
    }

    /// <summary>The "Example card" record fills the card number, code, expiry, and cardholder, has no login, and keeps the PIN as a hidden field and the username as a text field.</summary>
    [Fact]
    public void Read_bank_card_fills_card_data_and_keeps_pin_and_login_as_fields()
    {
        // Act
        var items = ReadCommander().Items;

        // Assert
        var item = items.ShouldHaveSingleItem(item => item.Name == "Example card");
        var card = item.Card.ShouldNotBeNull();
        card.Number.ShouldBe("4111111111111111");
        card.Code.ShouldBe("123");
        card.ExpMonth.ShouldBe("03");
        card.ExpYear.ShouldBe("2030");
        card.CardholderName.ShouldBe("Alice Example");
        item.Login.ShouldBeNull();
        item.Fields.ShouldBe([Field("PIN", "4321", CustomFieldType.Hidden), Field("Username", "card-user")]);
    }

    /// <summary>The "Checking account" record keeps its combined account and routing numbers in a hidden field and its name in a text field.</summary>
    [Fact]
    public void Read_bank_account_keeps_account_numbers_hidden_in_bitwarden_format()
    {
        // Act
        var items = ReadCommander().Items;

        // Assert
        var item = items.ShouldHaveSingleItem(item => item.Name == "Checking account");
        item.Fields.ShouldBe(
        [
            Field("bankAccount", "Type: Checking, Account Number: 12345678, Routing Number: 021000021", CustomFieldType.Hidden),
            Field("name", "Alice Example"),
        ]);
    }

    /// <summary>The address, contact, passport, social security, driver license, and birth certificate records fill the matching identity slots, with leftover values such as dates and a second phone kept as fields.</summary>
    [Fact]
    public void Read_identity_records_fill_identity_slots()
    {
        // Act
        var items = ReadCommander().Items;

        // Assert
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

    /// <summary>Records such as the secure note, editor license, health plan, server, database, PAM machine, and custom API record keep their values as text, hidden, or date fields, and the scanned file keeps its attachment name.</summary>
    [Fact]
    public void Read_maps_hidden_text_and_date_fields_of_other_record_types()
    {
        // Act
        var items = ReadCommander().Items;

        // Assert
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

    /// <summary>An SSH key record without a private key has no SSH key data and keeps the username, hidden passphrase, and public key as fields.</summary>
    [Fact]
    public void Read_ssh_key_record_without_private_key_becomes_note_with_passphrase_and_public_key()
    {
        // Act
        var items = ReadCommander().Items;

        // Assert
        var item = items.ShouldHaveSingleItem(item => item.Name == "Key without private part");
        item.SshKey.ShouldBeNull();
        item.Fields.ShouldBe(
        [
            Field("Username", "deploy"),
            Field("Passphrase", "fixture-passphrase", CustomFieldType.Hidden),
            Field("Public key", "ssh-ed25519 AAAAfixture deploy@example"),
        ]);
    }

    /// <summary>An SSH key record holding a generated ECDSA private key becomes an SSH key item with that key and the public key, and keeps the username, hidden password, hostname, and port as fields.</summary>
    [Fact]
    public void Read_ssh_key_record_with_private_key_becomes_ssh_key_item()
    {
        // Arrange
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string pem = key.ExportPkcs8PrivateKeyPem();

        // Act
        var items = KeeperJsonReader.Read(SshKeyRecord(pem, "ecdsa-sha2-nistp256 AAAAfixture"), "ssh.json").Export.Items;

        // Assert
        var item = items.ShouldHaveSingleItem();
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

    /// <summary>A Keeper passkey becomes a FIDO2 credential with the b64-prefixed ID, relying party, user name and handle, counter, creation date, and an unpadded key that signs data which the original public key verifies.</summary>
    [Fact]
    public void Read_passkey_becomes_fido2_credential_whose_key_signs_for_the_original_public_key()
    {
        // Arrange
        using var original = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ECParameters parameters = original.ExportParameters(includePrivateParameters: true);
        byte[] credentialId = RandomNumberGenerator.GetBytes(16);

        // Act
        var items = KeeperJsonReader.Read(LoginWithPasskey(PasskeyJwk(parameters), Base64Url(credentialId)), "passkey.json").Export.Items;

        // Assert
        var item = items.ShouldHaveSingleItem();
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

    /// <summary>A passkey whose private scalar is not valid yields no FIDO2 credential, one hidden "passkey" field, and one warning that repeats neither the invalid nor the original scalar.</summary>
    [Fact]
    public void Read_invalid_passkey_is_kept_as_hidden_field_and_warns_without_values()
    {
        // Arrange
        using var original = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var jwk = PasskeyJwk(original.ExportParameters(includePrivateParameters: true));
        string privateScalar = jwk["d"]!.GetValue<string>();
        jwk["d"] = "not-a-scalar";

        // Act
        var result = KeeperJsonReader.Read(LoginWithPasskey(jwk, "AAAA"), "passkey.json");

        // Assert
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

    /// <summary>The web.json export maps its numbered record keys to the origin ID without a timestamp, unescapes the folder path, converts the legacy TOTP into a hidden field, and names an untitled note "--".</summary>
    [Fact]
    public void Read_web_export_parses_numbered_keys_legacy_totp_escaped_folders_and_has_no_timestamp()
    {
        // Act
        var export = KeeperJsonReader.Read(Fixture.Read("keeper/web.json"), "web.json").Export;

        // Assert
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

    /// <summary>A JSON array of records without the surrounding object reads as one login item named "Bare".</summary>
    [Fact]
    public void Read_accepts_bare_record_array()
    {
        // Act
        var items = KeeperJsonReader.Read("""[{"uid":"a","title":"Bare","login":"u"}]""", "bare.json").Export.Items;

        // Assert
        var item = items.ShouldHaveSingleItem();
        item.Name.ShouldBe("Bare");
        item.Type.ShouldBe(VaultItemType.Login);
    }

    /// <summary>Text that is not JSON throws InvalidDataException.</summary>
    [Fact]
    public void Read_invalid_JSON_throws_invalid_data()
    {
        // Arrange
        var action = () => KeeperJsonReader.Read("{ not json", "bad.json");

        // Act, Assert
        action.ShouldThrow<InvalidDataException>();
    }

    /// <summary>In a zip export the listed scan.pdf attaches with its size and entry path, and the listed but absent missing.txt attaches without them and causes one warning that names it.</summary>
    [Fact]
    public void ReadZip_attaches_files_by_file_uid_and_warns_about_missing_files()
    {
        // Arrange
        string path = Path.Combine(directory, "keeper.zip");
        File.WriteAllBytes(path, TestZip.Create(("export.json", TestZip.Utf8(CommanderZipExport())), ("files/file-1", "%PDF"u8.ToArray())));

        // Act
        var result = KeeperJsonReader.ReadZip(path);

        // Assert
        var item = result.Export.Items.ShouldHaveSingleItem();
        item.Origin.SourceFile.ShouldBe("keeper.zip");
        item.Attachments.ShouldBe([new AttachmentRef("scan.pdf", 4, "files/file-1"), new AttachmentRef("missing.txt", null, null)]);
        result.Warnings.ShouldHaveSingleItem().ShouldContain("missing.txt", StringComparison.Ordinal);
    }

    /// <summary>A zip warns, in order, about the listed attachment that is missing from the zip, the file entry no record lists, and the entry outside the attachment folder that is ignored.</summary>
    [Fact]
    public void ReadZip_warns_about_files_no_record_lists_and_about_other_entries()
    {
        // Arrange
        string path = Path.Combine(directory, "keeper.zip");
        File.WriteAllBytes(path, TestZip.Create(
            ("export.json", TestZip.Utf8(CommanderZipExport())),
            ("files/file-1", "%PDF"u8.ToArray()),
            ("files/fu-orphan", TestZip.Utf8("fixture orphan")),
            ("other/stray.txt", TestZip.Utf8("fixture stray"))));

        // Act
        var result = KeeperJsonReader.ReadZip(path);

        // Assert
        result.Warnings.ShouldBe(
        [
            "\"Record with files\": attachment \"missing.txt\" is listed but not included in the zip.",
            "Zip entry \"files/fu-orphan\" belongs to no record; upload it by hand to the item it belongs to after the import.",
            "Zip entry \"other/stray.txt\" is not a Keeper attachment and was ignored.",
        ]);
    }

    /// <summary>A zip that holds only data.json and no export.json throws InvalidDataException.</summary>
    [Fact]
    public void ReadZip_without_export_json_throws_invalid_data()
    {
        // Arrange
        string path = Path.Combine(directory, "other.zip");
        File.WriteAllBytes(path, TestZip.Create(("data.json", TestZip.Utf8("{}"))));
        var action = () => KeeperJsonReader.ReadZip(path);

        // Act, Assert
        action.ShouldThrow<InvalidDataException>();
    }

    /// <summary>The exception for a records value of the wrong type, the commander.json warnings, and its conversion notes never contain any fixture secret value.</summary>
    [Fact]
    public void Read_warnings_and_errors_never_contain_values()
    {
        // Arrange
        var action = () => KeeperJsonReader.Read("""{"records":"fixture-password-1"}""", "bad.json");

        // Act
        var result = KeeperJsonReader.Read(Fixture.Read("keeper/commander.json"), "commander.json");
        var exception = action.ShouldThrow<InvalidDataException>();

        // Assert
        exception.Message.ShouldNotContain("fixture-password-1", StringComparison.Ordinal);
        result.Warnings.ShouldAllSatisfy(warning => warning.ShouldNotContain("fixture", StringComparison.Ordinal));
        result.Export.Items.SelectMany(item => item.ConversionNotes)
            .ShouldAllSatisfy(note => note.ShouldNotContain("fixture", StringComparison.Ordinal));
    }
}
