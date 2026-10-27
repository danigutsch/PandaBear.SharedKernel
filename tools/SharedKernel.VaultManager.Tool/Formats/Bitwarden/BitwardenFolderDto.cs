namespace PandaBear.VaultManager.Tool.Formats.Bitwarden;

/// <summary>Bitwarden folder or collection.</summary>
/// <param name="Id">Folder or collection id.</param>
/// <param name="Name">Display name.</param>
internal sealed record BitwardenFolderDto(string? Id, string? Name);
