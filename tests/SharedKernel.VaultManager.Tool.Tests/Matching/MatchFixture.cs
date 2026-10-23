using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Tests.Matching;

/// <summary>Builds small items for matcher tests.</summary>
internal static class MatchFixture
{
    public static VaultItem Login(string id, string name, string? username, params string[] uris) => new()
    {
        Type = VaultItemType.Login,
        Name = name,
        Login = new LoginData { Username = username, Password = "fixture-" + id, Uris = [.. uris.Select(uri => new LoginUri(uri))] },
        Origin = new ItemOrigin("bitwarden", "vault.json", id, null),
    };

    public static VaultItem Note(string? id, string name) => new()
    {
        Type = VaultItemType.SecureNote,
        Name = name,
        Notes = "fixture note " + name,
        Origin = new ItemOrigin("keeper", "keeper.json", id, null),
    };
}
