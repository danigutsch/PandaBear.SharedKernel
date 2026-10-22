using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Tests.Model;

public sealed class RedactionTests
{
    private const string Secret = "fixture-secret-value";

    [Fact]
    public void ToString_of_secret_bearing_records_never_contains_values()
    {
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

        printed.ShouldAllSatisfy(text => text.ShouldNotContain(Secret, StringComparison.Ordinal));
        printed[0].ShouldContain("visible name", StringComparison.Ordinal);
    }
}
