using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Formats.Bitwarden;

/// <summary>Bitwarden export item.</summary>
internal sealed record BitwardenItemDto
{
    /// <summary>Item id.</summary>
    public string? Id
    {
        get; init;
    }

    /// <summary>Id of the owning organization, or null for a personal item.</summary>
    public string? OrganizationId
    {
        get; init;
    }

    /// <summary>Id of the folder that contains the item, or null.</summary>
    public string? FolderId
    {
        get; init;
    }

    /// <summary>Item type: 1 login, 2 secure note, 3 card, 4 identity, 5 SSH key.</summary>
    public int Type
    {
        get; init;
    }

    /// <summary>Whether the client asks for the master password before showing the item; 0 is off.</summary>
    public int Reprompt
    {
        get; init;
    }

    /// <summary>Item display name.</summary>
    public string? Name
    {
        get; init;
    }

    /// <summary>Free-text notes.</summary>
    public string? Notes
    {
        get; init;
    }

    /// <summary>Whether the item is marked as a favorite.</summary>
    public bool Favorite
    {
        get; init;
    }

    /// <summary>Custom fields of the item.</summary>
    public IReadOnlyList<CustomField>? Fields
    {
        get; init;
    }

    /// <summary>Login payload for login items.</summary>
    public BitwardenLoginDto? Login
    {
        get; init;
    }

    /// <summary>Secure note payload for note items.</summary>
    public BitwardenSecureNoteDto? SecureNote
    {
        get; init;
    }

    /// <summary>Card payload for card items.</summary>
    public CardData? Card
    {
        get; init;
    }

    /// <summary>Identity payload for identity items.</summary>
    public IdentityData? Identity
    {
        get; init;
    }

    /// <summary>SSH key payload for SSH key items.</summary>
    public SshKeyData? SshKey
    {
        get; init;
    }

    /// <summary>Ids of the collections the item belongs to.</summary>
    public IReadOnlyList<string>? CollectionIds
    {
        get; init;
    }

    /// <summary>Previous passwords of the item.</summary>
    public IReadOnlyList<PasswordHistoryEntry>? PasswordHistory
    {
        get; init;
    }

    /// <summary>When the item was last changed.</summary>
    public DateTimeOffset? RevisionDate
    {
        get; init;
    }

    /// <summary>When the item was created.</summary>
    public DateTimeOffset? CreationDate
    {
        get; init;
    }

    /// <summary>When the item was moved to the trash, written only for trashed items.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? DeletedDate
    {
        get; init;
    }

    /// <summary>Properties this tool does not model, such as newer item type payloads.</summary>
    [JsonExtensionData]
    public JsonObject? Extra
    {
        get; set;
    }
}
