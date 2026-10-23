using System.Text;
using System.Text.Json.Nodes;
using PandaBear.VaultManager.Tool.Crypto;
using PandaBear.VaultManager.Tool.Formats.Bitwarden;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Tests.Formats.Bitwarden;

public sealed class BitwardenJsonWriterTests
{
    private static readonly ItemOrigin Origin = new("test", "test.json", null, null);

    [Fact]
    public void Write_then_read_round_trips_every_item_type()
    {
        var original = Fixture.ReadBitwardenFull();

        var result = BitwardenJsonWriter.Write(original);
        var reread = BitwardenJsonReader.Read(result.Json, "written.json");

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

    [Fact]
    public void Write_emits_shared_folder_IDs_and_secure_note_object()
    {
        VaultItem[] items =
        [
            new() { Type = VaultItemType.SecureNote, Name = "a", FolderPath = "Work", Origin = Origin },
            new() { Type = VaultItemType.SecureNote, Name = "b", FolderPath = "Work", Origin = Origin },
        ];

        var document = JsonNode.Parse(BitwardenJsonWriter.Write(new VaultExport(items)).Json).ShouldNotBeNull();

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

    [Fact]
    public void Write_moves_history_beyond_five_entries_into_hidden_fields()
    {
        var start = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        PasswordHistoryEntry[] history = [.. Enumerable.Range(0, 7).Select(day => new PasswordHistoryEntry($"old-{day}", start.AddDays(day)))];
        var item = new VaultItem { Type = VaultItemType.Login, Name = "many", Login = new LoginData(), PasswordHistory = history, Origin = Origin };

        var result = BitwardenJsonWriter.Write(new VaultExport([item]));
        var written = BitwardenJsonReader.Read(result.Json, "w.json").Items.ShouldHaveSingleItem();

        written.PasswordHistory.Select(entry => entry.Password).ShouldBe(["old-6", "old-5", "old-4", "old-3", "old-2"]);
        written.Fields.Select(field => field.Value).ShouldBe(["old-1", "old-0"]);
        written.Fields.ShouldAllSatisfy(field => field.Type.ShouldBe(CustomFieldType.Hidden));
        result.Warnings.ShouldHaveSingleItem();
    }

    [Fact]
    public void Write_reports_values_over_encrypted_limits_without_truncating()
    {
        var longNotes = new string('n', 8_000);
        var item = new VaultItem { Type = VaultItemType.SecureNote, Name = "long", Notes = longNotes, Origin = Origin };

        var result = BitwardenJsonWriter.Write(new VaultExport([item]));

        result.Errors.ShouldHaveSingleItem().ShouldContain("notes", StringComparison.Ordinal);
        BitwardenJsonReader.Read(result.Json, "w.json").Items.ShouldHaveSingleItem().Notes.ShouldBe(longNotes);
    }

    [Fact]
    public void Write_errors_never_contain_values()
    {
        var secret = new string('s', 4_000);
        var item = new VaultItem
        {
            Type = VaultItemType.Login,
            Name = "secretive",
            Login = new LoginData { Password = secret },
            Fields = [new CustomField("token", secret, CustomFieldType.Hidden)],
            Origin = Origin,
        };

        var result = BitwardenJsonWriter.Write(new VaultExport([item]));

        result.Errors.Count.ShouldBe(2);
        result.Errors.ShouldAllSatisfy(error => error.ShouldNotContain(secret, StringComparison.Ordinal));
    }

    [Fact]
    public void Write_errors_name_custom_fields_with_long_names_by_position_only()
    {
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

        var result = BitwardenJsonWriter.Write(new VaultExport([item]));

        result.Errors.Count.ShouldBe(3);
        result.Errors[0].ShouldStartWith("\"Keeper import\": the name of custom field 2 is too long", StringComparison.Ordinal);
        result.Errors[1].ShouldStartWith("\"Keeper import\": the value of custom field 3 is too long", StringComparison.Ordinal);
        result.Errors[2].ShouldStartWith("\"Keeper import\": the value of custom field 4 \"API key\" is too long", StringComparison.Ordinal);
        result.Errors.ShouldAllSatisfy(error => error.ShouldNotContain("kkkk", StringComparison.Ordinal));
        result.Errors.ShouldAllSatisfy(error => error.ShouldNotContain("third grade", StringComparison.Ordinal));
    }

    [Fact]
    public void Write_warns_when_the_estimated_import_request_of_many_short_values_exceeds_the_vaultwarden_limit()
    {
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

        var result = BitwardenJsonWriter.Write(new VaultExport(items));

        long thirdLargerJson = Encoding.UTF8.GetByteCount(result.Json) * 4L / 3;
        thirdLargerJson.ShouldBeLessThan(BitwardenLimits.VaultwardenImportBytes);
        string warning = result.Warnings.ShouldHaveSingleItem();
        warning.ShouldContain("estimated", StringComparison.Ordinal);
        warning.ShouldContain("20,000,000 bytes", StringComparison.Ordinal);
        warning.ShouldNotContain("split", StringComparison.Ordinal);
        BitwardenLimits.VaultwardenImportBytes.ShouldBe(20_000_000);
    }

    [Fact]
    public void Write_does_not_warn_about_the_import_request_of_an_export_well_under_the_limit()
    {
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

        var result = BitwardenJsonWriter.Write(new VaultExport(items));

        result.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void Write_counts_the_encrypted_checksum_that_a_client_sends_with_each_login_URI()
    {
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

        var result = BitwardenJsonWriter.Write(new VaultExport(items));

        long compactJson = Encoding.UTF8.GetByteCount(JsonNode.Parse(result.Json).ShouldNotBeNull().ToJsonString());
        long withoutChecksums = compactJson + (items.Length * 51 * 95L);
        withoutChecksums.ShouldBeLessThan(BitwardenLimits.VaultwardenImportBytes);
        result.Warnings.ShouldHaveSingleItem().ShouldContain("estimated", StringComparison.Ordinal);
    }

    [Fact]
    public void Write_estimates_the_import_request_from_compact_JSON_without_the_indentation_of_the_export()
    {
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

        var result = BitwardenJsonWriter.Write(new VaultExport(items));

        long withIndentation = Encoding.UTF8.GetByteCount(result.Json) + (items.Length * 95L);
        withIndentation.ShouldBeGreaterThan(BitwardenLimits.VaultwardenImportBytes);
        result.Warnings.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("exactly sixteen!")]
    [InlineData("ünïcødé value with multibyte characters")]
    public void EncryptedLength_matches_actual_encrypted_string_length(string value)
    {
        var envelope = PasswordProtectedExport.Encrypt(value, "pw", new KdfConfig(KdfType.Pbkdf2Sha256, 5_000));

        var data = JsonNode.Parse(envelope).ShouldNotBeNull()["data"].ShouldNotBeNull().GetValue<string>();

        BitwardenLimits.EncryptedLength(value).ShouldBe(data.Length);
    }
}
