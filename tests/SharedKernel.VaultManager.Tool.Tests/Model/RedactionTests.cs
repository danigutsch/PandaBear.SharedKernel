using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Tests.Model;

/// <summary>Tests that <c>ToString</c> on vault model records hides secret values.</summary>
public sealed class RedactionTests
{
    private const string Secret = "fixture-secret-value";

    /// <summary>The printed item, custom field, login, passkey, card, identity, SSH key, and password history entry never contain the secret, while the item still prints its visible name.</summary>
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

    /// <summary>A printed attachment reference shows its file name but not its SHA-256 content hash.</summary>
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
}
