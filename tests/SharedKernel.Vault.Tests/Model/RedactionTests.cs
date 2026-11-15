using PandaBear.Vault.Brief;
using PandaBear.Vault.Catalog;
using PandaBear.Vault.Formats;
using PandaBear.Vault.Merge;
using PandaBear.Vault.Model;

namespace PandaBear.Vault.Tests.Model;

/// <summary>Tests that <c>ToString</c> on public vault records hides secret values.</summary>
public sealed class RedactionTests
{
    private const string Secret = "fixture-secret-value";

    [Fact]
    public void ToString_of_secret_bearing_records_never_contains_values()
    {
        // Arrange
        var login = new LoginData
        {
            Username = "user",
            Password = Secret,
            Totp = Secret,
            Fido2Credentials = [new Fido2Credential { CredentialId = Secret, KeyValue = Secret, RpId = "example.com" }],
        };
        var item = new VaultItem
        {
            Type = VaultItemType.Login,
            Name = "visible name",
            Notes = Secret,
            Fields = [new CustomField("label", Secret, CustomFieldType.Hidden)],
            Login = login,
            Card = new CardData { Number = Secret, Code = Secret },
            Identity = new IdentityData { Ssn = Secret },
            SshKey = new SshKeyData { PrivateKey = Secret },
            PasswordHistory = [new PasswordHistoryEntry(Secret, DateTimeOffset.UnixEpoch)],
            Origin = new ItemOrigin("test", "file.json", "id", null),
        };

        // Act
        string[] printed =
        [
            item.ToString(),
            item.Fields[0].ToString(),
            login.ToString(),
            login.Fido2Credentials[0].ToString(),
            item.Card.ToString(),
            item.Identity.ToString(),
            item.SshKey.ToString(),
            item.PasswordHistory[0].ToString(),
        ];

        // Assert
        printed.ShouldAllSatisfy(text => text.ShouldNotContain(Secret, StringComparison.Ordinal));
        printed[0].ShouldContain("visible name", StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_of_an_attachment_never_contains_its_content_hash()
    {
        // Arrange
        const string Hash = "3f79bb7b435b05321651daefd374cdc681dc06faa65e374e38337b88ca046dea";
        var attachment = new AttachmentRef("codes.txt", 20, "files/file-1") { ContentHash = Hash };

        // Act
        string printed = attachment.ToString();

        // Assert
        printed.ShouldNotContain(Hash, StringComparison.Ordinal);
        printed.ShouldContain("codes.txt", StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_of_a_write_result_never_contains_the_export()
    {
        // Arrange
        var written = new VaultWriteResult("{\"items\":[{\"password\":\"" + Secret + "\"}]}", ["error"], []);

        // Act
        string printed = written.ToString();

        // Assert
        printed.ShouldNotContain(Secret, StringComparison.Ordinal);
        printed.ShouldNotContain("items", StringComparison.Ordinal);
        printed.ShouldContain("Errors = 1", StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_of_public_records_with_addresses_user_names_or_field_values_never_contains_them()
    {
        // Arrange
        const string Address = "https://user:" + Secret + "@example.com/?token=" + Secret;
        const string Username = "fixture-user-name";

        // Act
        string[] printed =
        [
            new LoginUri(Address).ToString(),
            new CatalogUri(Value: "https://" + Secret + ".example.com").ToString(),
            new CatalogField("label", Secret).ToString(),
            new CatalogNotes(NotesDecision.Share, Secret).ToString(),
            new CatalogInfo(VaultItemType.Login, Username, true, false, 0, 0, [], []).ToString(),
            new BriefMember("key", "file.json", "visible name", null, "example.com", Username, null, false, []).ToString(),
        ];

        // Assert
        printed.ShouldAllSatisfy(text => text.ShouldNotContain(Secret, StringComparison.Ordinal));
        printed.ShouldAllSatisfy(text => text.ShouldNotContain(Username, StringComparison.Ordinal));
        printed[2].ShouldContain("label", StringComparison.Ordinal);
        printed[5].ShouldContain("visible name", StringComparison.Ordinal);
    }
}
