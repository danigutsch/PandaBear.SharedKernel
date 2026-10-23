using System.Text;

namespace PandaBear.VaultManager.Tool.Model;

/// <summary>Provider-neutral vault item, shaped like a Bitwarden export item.</summary>
internal sealed record VaultItem
{
    /// <summary>Item kind.</summary>
    public required VaultItemType Type
    {
        get; init;
    }

    /// <summary>Item name.</summary>
    public required string Name
    {
        get; init;
    }

    /// <summary>Folder path with <c>/</c> separators, or <see langword="null"/> for no folder.</summary>
    public string? FolderPath
    {
        get; init;
    }

    /// <summary>Whether the item is marked as favorite.</summary>
    public bool Favorite
    {
        get; init;
    }

    /// <summary>Whether the vault asks for the master password before showing the item.</summary>
    public bool Reprompt
    {
        get; init;
    }

    /// <summary>Notes.</summary>
    public string? Notes
    {
        get; init;
    }

    /// <summary>Custom fields.</summary>
    public IReadOnlyList<CustomField> Fields { get; init; } = [];

    /// <summary>Login data for <see cref="VaultItemType.Login"/> items.</summary>
    public LoginData? Login
    {
        get; init;
    }

    /// <summary>Card data for <see cref="VaultItemType.Card"/> items.</summary>
    public CardData? Card
    {
        get; init;
    }

    /// <summary>Identity data for <see cref="VaultItemType.Identity"/> items.</summary>
    public IdentityData? Identity
    {
        get; init;
    }

    /// <summary>Key data for <see cref="VaultItemType.SshKey"/> items.</summary>
    public SshKeyData? SshKey
    {
        get; init;
    }

    /// <summary>Former passwords, newest first.</summary>
    public IReadOnlyList<PasswordHistoryEntry> PasswordHistory { get; init; } = [];

    /// <summary>Creation time reported by the source.</summary>
    public DateTimeOffset? CreationDate
    {
        get; init;
    }

    /// <summary>Last revision time reported by the source.</summary>
    public DateTimeOffset? RevisionDate
    {
        get; init;
    }

    /// <summary>Where the item was read from.</summary>
    public required ItemOrigin Origin
    {
        get; init;
    }

    /// <summary>Files attached in the source vault. Bitwarden JSON imports cannot carry them.</summary>
    public IReadOnlyList<AttachmentRef> Attachments { get; init; } = [];

    /// <summary>Transformations or losses that happened while reading the item.</summary>
    public IReadOnlyList<string> ConversionNotes { get; init; } = [];

    /// <summary>Latest known modification time: the source modification time, else the revision date.</summary>
    public DateTimeOffset? LastModified => Origin.Modified ?? RevisionDate;

    /// <summary>Prints only non-secret members, so values never reach logs or messages through <c>ToString</c>.</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Type = ").Append(Type).Append(", Name = ").Append(Name).Append(", FolderPath = ").Append(FolderPath).Append(", Origin = ").Append(Origin);
        return true;
    }
}
