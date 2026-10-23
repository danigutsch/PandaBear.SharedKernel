using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Formats.Bitwarden;

/// <summary>Bitwarden export item.</summary>
internal sealed record BitwardenItemDto
{
    public string? Id
    {
        get; init;
    }

    public string? OrganizationId
    {
        get; init;
    }

    public string? FolderId
    {
        get; init;
    }

    public int Type
    {
        get; init;
    }

    public int Reprompt
    {
        get; init;
    }

    public string? Name
    {
        get; init;
    }

    public string? Notes
    {
        get; init;
    }

    public bool Favorite
    {
        get; init;
    }

    public IReadOnlyList<CustomField>? Fields
    {
        get; init;
    }

    public BitwardenLoginDto? Login
    {
        get; init;
    }

    public BitwardenSecureNoteDto? SecureNote
    {
        get; init;
    }

    public CardData? Card
    {
        get; init;
    }

    public IdentityData? Identity
    {
        get; init;
    }

    public SshKeyData? SshKey
    {
        get; init;
    }

    public IReadOnlyList<string>? CollectionIds
    {
        get; init;
    }

    public IReadOnlyList<PasswordHistoryEntry>? PasswordHistory
    {
        get; init;
    }

    public DateTimeOffset? RevisionDate
    {
        get; init;
    }

    public DateTimeOffset? CreationDate
    {
        get; init;
    }

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
