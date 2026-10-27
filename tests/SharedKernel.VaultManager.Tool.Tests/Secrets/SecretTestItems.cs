using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

/// <summary>Builds synthetic vault items for secret scanner tests.</summary>
internal static class SecretTestItems
{
    /// <summary>Origin shared by all items built here.</summary>
    public static ItemOrigin Origin { get; } = new("test", "test.json", null, null);

    /// <summary>Builds a login item with the given notes, credentials, optional address, and custom fields.</summary>
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

    /// <summary>Builds a secure note item with the given notes and name.</summary>
    public static VaultItem Note(string notes, string name = "Note")
    {
        return new VaultItem { Type = VaultItemType.SecureNote, Name = name, Notes = notes, Origin = Origin };
    }
}
