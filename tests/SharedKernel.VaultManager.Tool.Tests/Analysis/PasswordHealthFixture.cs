using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Tests.Analysis;

/// <summary>Builds login items for password health tests.</summary>
internal static class PasswordHealthFixture
{
    /// <summary>Creates a login with the given name, user name, password, and one address, originating from a Bitwarden export.</summary>
    public static VaultItem Item(string name, string username, string password, string uri) => new()
    {
        Type = VaultItemType.Login,
        Name = name,
        Login = new LoginData { Username = username, Password = password, Uris = [new LoginUri(uri)] },
        Origin = new ItemOrigin("bitwarden", "vault.json", name, null),
    };
}
