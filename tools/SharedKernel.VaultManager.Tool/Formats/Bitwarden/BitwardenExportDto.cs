using System.Text.Json.Serialization;

namespace PandaBear.VaultManager.Tool.Formats.Bitwarden;

/// <summary>Bitwarden JSON export document, unencrypted or account-restricted.</summary>
internal sealed record BitwardenExportDto
{
    /// <summary>Whether the export content is encrypted.</summary>
    public bool Encrypted
    {
        get; init;
    }

    /// <summary>Whether the export is protected by a password, written only for password-protected exports.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? PasswordProtected
    {
        get; init;
    }

    /// <summary>Folders of the vault.</summary>
    public IReadOnlyList<BitwardenFolderDto>? Folders
    {
        get; init;
    }

    /// <summary>Collections of the vault, written only for organization exports.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<BitwardenFolderDto>? Collections
    {
        get; init;
    }

    /// <summary>Vault items.</summary>
    public IReadOnlyList<BitwardenItemDto>? Items
    {
        get; init;
    }
}
