using System.Text;
using System.Text.Json.Nodes;
using PandaBear.VaultManager.Tool.Crypto;
using PandaBear.VaultManager.Tool.Formats.Bitwarden;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Tests.Formats.Bitwarden;

/// <summary>Tests the writer for Bitwarden JSON exports, including its size limits and warnings.</summary>
public sealed class BitwardenJsonWriterTests
{
    private static readonly ItemOrigin Origin = new("test", "test.json", null, null);

    /// <summary>Writing the full.json vault and reading the result back reports no errors and keeps each item's type, name, folder, notes, flags, fields, login data, card, identity, SSH key, and password history.</summary>
    [Fact]
    public void Write_then_read_round_trips_every_item_type()
    {
        // Arrange
        var original = Fixture.ReadBitwardenFull();

        // Act
        var result = BitwardenJsonWriter.Write(original);
        var reread = BitwardenJsonReader.Read(result.Json, "written.json");

        // Assert
        result.Errors.ShouldBeEmpty();
        reread.Items.Count.ShouldBe(original.Items.Count);
        foreach (var (before, after) in original.Items.Zip(reread.Items))
        {
            after.Type.ShouldBe(before.Type);
            after.Name.ShouldBe(before.Name);
            after.FolderPath.ShouldBe(before.FolderPath);
            after.Notes.ShouldBe(before.Notes);
            after.Favorite.ShouldBe(before.Favorite);
            after.Reprompt.ShouldBe(before.Reprompt);
            after.Fields.ShouldBe(before.Fields);
            var afterPassword = after.Login?.Password;
            afterPassword.ShouldBe(before.Login?.Password);
            var afterTotp = after.Login?.Totp;
            afterTotp.ShouldBe(before.Login?.Totp);
            var afterUris = after.Login?.Uris ?? [];
            afterUris.ShouldBe(before.Login?.Uris ?? []);
            var afterPasskeys = after.Login?.Fido2Credentials ?? [];
            afterPasskeys.ShouldBe(before.Login?.Fido2Credentials ?? []);
            after.Card.ShouldBe(before.Card);
            after.Identity.ShouldBe(before.Identity);
            after.SshKey.ShouldBe(before.SshKey);
            after.PasswordHistory.ShouldBe(before.PasswordHistory);
        }
    }

    /// <summary>Two secure notes in the folder "Work" produce one folder entry that both items reference by ID, a secureNote object of type 0 on each item, and an unencrypted export.</summary>
    [Fact]
    public void Write_emits_shared_folder_IDs_and_secure_note_object()
    {
        // Arrange
        VaultItem[] items =
        [
            new() { Type = VaultItemType.SecureNote, Name = "a", FolderPath = "Work", Origin = Origin },
            new() { Type = VaultItemType.SecureNote, Name = "b", FolderPath = "Work", Origin = Origin },
        ];

        // Act
        var result = BitwardenJsonWriter.Write(new VaultExport(items));

        // Assert
        var document = JsonNode.Parse(result.Json).ShouldNotBeNull();
        var folder = document["folders"].ShouldNotBeNull().AsArray().ShouldHaveSingleItem().ShouldNotBeNull();
        folder["name"].ShouldNotBeNull().GetValue<string>().ShouldBe("Work");
        var folderId = folder["id"].ShouldNotBeNull().GetValue<string>();
        document["items"].ShouldNotBeNull().AsArray().ShouldAllSatisfy(item =>
        {
            var element = item.ShouldNotBeNull();
            element["folderId"].ShouldNotBeNull().GetValue<string>().ShouldBe(folderId);
            element["secureNote"].ShouldNotBeNull()["type"].ShouldNotBeNull().GetValue<int>().ShouldBe(0);
        });
        document["encrypted"].ShouldNotBeNull().GetValue<bool>().ShouldBeFalse();
    }

    /// <summary>An item in "Team/Shared/Web" and a declared empty folder "Archive/Old" yield folder entries for every parent path in sorted order.</summary>
    [Fact]
    public void Write_emits_parent_folders_and_declared_folders_that_hold_no_item()
    {
        // Arrange
        VaultItem[] items = [new() { Type = VaultItemType.SecureNote, Name = "a", FolderPath = "Team/Shared/Web", Origin = Origin }];
        var export = new VaultExport(items) { Folders = ["Archive/Old"] };

        // Act
        var result = BitwardenJsonWriter.Write(export);

        // Assert
        var document = JsonNode.Parse(result.Json).ShouldNotBeNull();
        document["folders"].ShouldNotBeNull().AsArray()
            .Select(folder => folder.ShouldNotBeNull()["name"].ShouldNotBeNull().GetValue<string>())
            .ShouldBe(["Archive", "Archive/Old", "Team", "Team/Shared", "Team/Shared/Web"]);
    }

    /// <summary>A vault with no items and the declared folder "Empty" still has that folder after it is written and read back.</summary>
    [Fact]
    public void Write_then_read_keeps_folders_that_hold_no_item()
    {
        // Arrange
        var export = new VaultExport([]) { Folders = ["Empty"] };

        // Act
        var reread = BitwardenJsonReader.Read(BitwardenJsonWriter.Write(export).Json, "written.json");

        // Assert
        reread.Folders.ShouldBe(["Empty"]);
    }

    /// <summary>Of seven password history entries the five newest stay as history and the two oldest become hidden fields, with one warning.</summary>
    [Fact]
    public void Write_moves_history_beyond_five_entries_into_hidden_fields()
    {
        // Arrange
        var start = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        PasswordHistoryEntry[] history = [.. Enumerable.Range(0, 7).Select(day => new PasswordHistoryEntry($"old-{day}", start.AddDays(day)))];
        var item = new VaultItem { Type = VaultItemType.Login, Name = "many", Login = new LoginData(), PasswordHistory = history, Origin = Origin };

        // Act
        var result = BitwardenJsonWriter.Write(new VaultExport([item]));
        var rereadItems = BitwardenJsonReader.Read(result.Json, "w.json").Items;

        // Assert
        var written = rereadItems.ShouldHaveSingleItem();
        written.PasswordHistory.Select(entry => entry.Password).ShouldBe(["old-6", "old-5", "old-4", "old-3", "old-2"]);
        written.Fields.Select(field => field.Value).ShouldBe(["old-1", "old-0"]);
        written.Fields.ShouldAllSatisfy(field => field.Type.ShouldBe(CustomFieldType.Hidden));
        result.Warnings.ShouldHaveSingleItem();
    }

    /// <summary>An 8,000-character note yields one error that mentions notes, while the written JSON still holds the full note.</summary>
    [Fact]
    public void Write_reports_values_over_encrypted_limits_without_truncating()
    {
        // Arrange
        var longNotes = new string('n', 8_000);
        var item = new VaultItem { Type = VaultItemType.SecureNote, Name = "long", Notes = longNotes, Origin = Origin };

        // Act
        var result = BitwardenJsonWriter.Write(new VaultExport([item]));

        // Assert
        result.Errors.ShouldHaveSingleItem().ShouldContain("notes", StringComparison.Ordinal);
        BitwardenJsonReader.Read(result.Json, "w.json").Items.ShouldHaveSingleItem().Notes.ShouldBe(longNotes);
    }

    /// <summary>A 4,000-character password and hidden field value produce two errors and neither contains the secret text.</summary>
    [Fact]
    public void Write_errors_never_contain_values()
    {
        // Arrange
        var secret = new string('s', 4_000);
        var item = new VaultItem
        {
            Type = VaultItemType.Login,
            Name = "secretive",
            Login = new LoginData { Password = secret },
            Fields = [new CustomField("token", secret, CustomFieldType.Hidden)],
            Origin = Origin,
        };

        // Act
        var result = BitwardenJsonWriter.Write(new VaultExport([item]));

        // Assert
        result.Errors.Count.ShouldBe(2);
        result.Errors.ShouldAllSatisfy(error => error.ShouldNotContain(secret, StringComparison.Ordinal));
    }

    /// <summary>Errors for an 800-character field name and a question-like field name identify the field by position only, while a short name such as "API key" may be quoted, and no error repeats the long text.</summary>
    [Fact]
    public void Write_errors_name_custom_fields_with_long_names_by_position_only()
    {
        // Arrange
        string pastedName = new('k', 800);
        const string Question = "What was the name of the street where you lived in third grade?";
        string longValue = new('v', 4_000);
        var item = new VaultItem
        {
            Type = VaultItemType.SecureNote,
            Name = "Keeper import",
            Fields =
            [
                new CustomField("Short", "fixture-value", CustomFieldType.Text),
                new CustomField(pastedName, "fixture-answer", CustomFieldType.Hidden),
                new CustomField(Question, longValue, CustomFieldType.Hidden),
                new CustomField("API key", longValue, CustomFieldType.Hidden),
            ],
            Origin = Origin,
        };

        // Act
        var result = BitwardenJsonWriter.Write(new VaultExport([item]));

        // Assert
        result.Errors.Count.ShouldBe(3);
        result.Errors[0].ShouldStartWith("\"Keeper import\": the name of custom field 2 is too long", StringComparison.Ordinal);
        result.Errors[1].ShouldStartWith("\"Keeper import\": the value of custom field 3 is too long", StringComparison.Ordinal);
        result.Errors[2].ShouldStartWith("\"Keeper import\": the value of custom field 4 \"API key\" is too long", StringComparison.Ordinal);
        result.Errors.ShouldAllSatisfy(error => error.ShouldNotContain("kkkk", StringComparison.Ordinal));
        result.Errors.ShouldAllSatisfy(error => error.ShouldNotContain("third grade", StringComparison.Ordinal));
    }

    /// <summary>5,000 items with 20 one-character custom fields give one warning about an estimated import request over 20,000,000 bytes even though the export JSON plus a third stays below that limit.</summary>
    [Fact]
    public void Write_warns_when_the_estimated_import_request_of_many_short_values_exceeds_the_vaultwarden_limit()
    {
        // Arrange
        // Each one-character value becomes a 96-character encrypted string in the import request, so 5,000 items with
        // 41 such values each need about 26 MB, although their export JSON takes about 14 MB.
        VaultItem[] items =
        [
            .. Enumerable.Range(0, 5_000).Select(_ => new VaultItem
            {
                Type = VaultItemType.SecureNote,
                Name = "n",
                Fields = [.. Enumerable.Range(0, 20).Select(position => new CustomField("f", "v", CustomFieldType.Text))],
                Origin = Origin,
            }),
        ];

        // Act
        var result = BitwardenJsonWriter.Write(new VaultExport(items));

        // Assert
        long thirdLargerJson = Encoding.UTF8.GetByteCount(result.Json) * 4L / 3;
        thirdLargerJson.ShouldBeLessThan(BitwardenLimits.VaultwardenImportBytes);
        string warning = result.Warnings.ShouldHaveSingleItem();
        warning.ShouldContain("estimated", StringComparison.Ordinal);
        warning.ShouldContain("20,000,000 bytes", StringComparison.Ordinal);
        warning.ShouldNotContain("split", StringComparison.Ordinal);
        BitwardenLimits.VaultwardenImportBytes.ShouldBe(20_000_000);
    }

    /// <summary>Only the login with two passkeys gets a warning, which starts with the item name and the passkey count and contains neither key values nor credential IDs, and there are no errors.</summary>
    [Fact]
    public void Write_warns_about_a_login_with_more_than_one_passkey_without_its_values()
    {
        // Arrange
        VaultItem[] items =
        [
            new()
            {
                Type = VaultItemType.Login,
                Name = "Shop",
                Login = new LoginData
                {
                    Fido2Credentials =
                    [
                        new Fido2Credential { CredentialId = "cred-k", KeyValue = "fixture-key-k", RpId = "shop.example" },
                        new Fido2Credential { CredentialId = "cred-b", KeyValue = "fixture-key-b", RpId = "shop.example" },
                    ],
                },
                Origin = Origin,
            },
            new()
            {
                Type = VaultItemType.Login,
                Name = "Mail",
                Login = new LoginData { Fido2Credentials = [new Fido2Credential { CredentialId = "cred-m", KeyValue = "fixture-key-m", RpId = "mail.example" }] },
                Origin = Origin,
            },
        ];

        // Act
        var result = BitwardenJsonWriter.Write(new VaultExport(items));

        // Assert
        string warning = result.Warnings.ShouldHaveSingleItem();
        warning.ShouldStartWith("\"Shop\": 2 passkeys", StringComparison.Ordinal);
        warning.ShouldNotContain("fixture-key", StringComparison.Ordinal);
        warning.ShouldNotContain("cred-", StringComparison.Ordinal);
        result.Errors.ShouldBeEmpty();
    }

    /// <summary>500 secure notes with 20 short custom fields each produce no warnings.</summary>
    [Fact]
    public void Write_does_not_warn_about_the_import_request_of_an_export_well_under_the_limit()
    {
        // Arrange
        VaultItem[] items =
        [
            .. Enumerable.Range(0, 500).Select(_ => new VaultItem
            {
                Type = VaultItemType.SecureNote,
                Name = "n",
                Fields = [.. Enumerable.Range(0, 20).Select(position => new CustomField("f", "v", CustomFieldType.Text))],
                Origin = Origin,
            }),
        ];

        // Act
        var result = BitwardenJsonWriter.Write(new VaultExport(items));

        // Assert
        result.Warnings.ShouldBeEmpty();
    }

    /// <summary>1,800 logins with 50 URIs each stay below the import limit without the 153-byte checksum per URI but produce an estimate warning once the checksums are counted.</summary>
    [Fact]
    public void Write_counts_the_encrypted_checksum_that_a_client_sends_with_each_login_URI()
    {
        // Arrange
        // A client sends the encrypted SHA-256 checksum of each website address with it, 153 bytes in the request. These
        // logins stay below the limit without the checksums and exceed it with them.
        VaultItem[] items =
        [
            .. Enumerable.Range(0, 1_800).Select(_ => new VaultItem
            {
                Type = VaultItemType.Login,
                Name = "n",
                Login = new LoginData { Uris = [.. Enumerable.Range(0, 50).Select(position => new LoginUri("u"))] },
                Origin = Origin,
            }),
        ];

        // Act
        var result = BitwardenJsonWriter.Write(new VaultExport(items));

        // Assert
        long compactJson = Encoding.UTF8.GetByteCount(JsonNode.Parse(result.Json).ShouldNotBeNull().ToJsonString());
        long withoutChecksums = compactJson + (items.Length * 51 * 95L);
        withoutChecksums.ShouldBeLessThan(BitwardenLimits.VaultwardenImportBytes);
        result.Warnings.ShouldHaveSingleItem().ShouldContain("estimated", StringComparison.Ordinal);
    }

    /// <summary>24,000 nearly empty identities give no warning because the estimate uses compact JSON, although the indented export would push it above the import limit.</summary>
    [Fact]
    public void Write_estimates_the_import_request_from_compact_JSON_without_the_indentation_of_the_export()
    {
        // Arrange
        // A client posts compact JSON. These identities hold no value but their name, so the indentation of the export is
        // a large part of it; counted, it would push the estimate above the limit.
        VaultItem[] items =
        [
            .. Enumerable.Range(0, 24_000).Select(_ => new VaultItem
            {
                Type = VaultItemType.Identity,
                Name = "n",
                Identity = new IdentityData(),
                Origin = Origin,
            }),
        ];

        // Act
        var result = BitwardenJsonWriter.Write(new VaultExport(items));

        // Assert
        long withIndentation = Encoding.UTF8.GetByteCount(result.Json) + (items.Length * 95L);
        withIndentation.ShouldBeGreaterThan(BitwardenLimits.VaultwardenImportBytes);
        result.Warnings.ShouldBeEmpty();
    }

    /// <summary>The computed encrypted length of a value equals the length of the data string that password-protected export encryption produces for it, including for empty, block-sized, and multibyte values.</summary>
    /// <param name="value">The plain text value to encrypt.</param>
    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("exactly sixteen!")]
    [InlineData("ünïcødé value with multibyte characters")]
    public void EncryptedLength_matches_actual_encrypted_string_length(string value)
    {
        // Arrange
        var envelope = PasswordProtectedExport.Encrypt(value, "pw", new KdfConfig(KdfType.Pbkdf2Sha256, 5_000));
        var data = JsonNode.Parse(envelope).ShouldNotBeNull()["data"].ShouldNotBeNull().GetValue<string>();

        // Act
        var length = BitwardenLimits.EncryptedLength(value);

        // Assert
        length.ShouldBe(data.Length);
    }
}
