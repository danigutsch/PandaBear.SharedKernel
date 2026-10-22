using System.Text;

namespace PandaBear.VaultManager.Tool.Formats.Keeper;

/// <summary>Keeper record as read from a JSON or CSV export, before mapping.</summary>
internal sealed record KeeperRecord
{
    /// <summary>Record identifier.</summary>
    public string? Uid { get; init; }

    /// <summary>Record type, for example <c>login</c>, or <see langword="null"/> for exports without types.</summary>
    public string? Type { get; init; }

    /// <summary>Title.</summary>
    public string? Title { get; init; }

    /// <summary>Login name.</summary>
    public string? Login { get; init; }

    /// <summary>Password.</summary>
    public string? Password { get; init; }

    /// <summary>Website address.</summary>
    public string? LoginUrl { get; init; }

    /// <summary>Notes.</summary>
    public string? Notes { get; init; }

    /// <summary>Commander modification time in Unix seconds.</summary>
    public long? LastModified { get; init; }

    /// <summary>Typed and custom fields in export order.</summary>
    public IReadOnlyList<KeeperField> Fields { get; init; } = [];

    /// <summary>Folder placements.</summary>
    public IReadOnlyList<KeeperFolder> Folders { get; init; } = [];

    /// <summary>Attached files.</summary>
    public IReadOnlyList<KeeperAttachment> Attachments { get; init; } = [];

    /// <summary>Number of references to other records.</summary>
    public int ReferenceCount { get; init; }

    /// <summary>Whether the export declared a field schema.</summary>
    public bool HasSchema { get; init; }

    /// <summary>Prints only identifying members, so values never reach logs or messages through <c>ToString</c>.</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Uid = ").Append(Uid).Append(", Type = ").Append(Type).Append(", Title = ").Append(Title);
        return true;
    }
}
