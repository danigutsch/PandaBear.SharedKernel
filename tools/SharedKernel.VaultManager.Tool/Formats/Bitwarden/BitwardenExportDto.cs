using System.Text.Json.Serialization;

namespace PandaBear.VaultManager.Tool.Formats.Bitwarden;

/// <summary>Bitwarden JSON export document, unencrypted or account-restricted.</summary>
internal sealed record BitwardenExportDto
{
    public bool Encrypted { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? PasswordProtected { get; init; }

    public IReadOnlyList<BitwardenFolderDto>? Folders { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<BitwardenFolderDto>? Collections { get; init; }

    public IReadOnlyList<BitwardenItemDto>? Items { get; init; }
}
