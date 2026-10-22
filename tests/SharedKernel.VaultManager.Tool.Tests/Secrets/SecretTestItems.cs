using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

internal static class SecretTestItems
{
    public static ItemOrigin Origin { get; } = new("test", "test.json", null, null);

    public static VaultItem Login(
        string? notes = null,
        string? password = null,
        string? username = null,
        string? totp = null,
        string name = "Example",
        string? uri = null,
        IReadOnlyList<CustomField>? fields = null)
    {
        return new VaultItem
        {
            Type = VaultItemType.Login,
            Name = name,
            Notes = notes,
            Fields = fields ?? [],
            Login = new LoginData
            {
                Username = username,
                Password = password,
                Totp = totp,
                Uris = uri is null ? [] : [new LoginUri(uri)],
            },
            Origin = Origin,
        };
    }

    public static VaultItem Note(string notes, string name = "Note")
    {
        return new VaultItem { Type = VaultItemType.SecureNote, Name = name, Notes = notes, Origin = Origin };
    }
}
