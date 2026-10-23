namespace PandaBear.VaultManager.Tool.Formats.Keeper;

/// <summary>Folder placement of a Keeper record.</summary>
/// <param name="SharedFolder">Shared folder path with backslash separators, or <see langword="null"/>.</param>
/// <param name="Folder">Folder path inside the shared folder, or the user folder path.</param>
/// <param name="CanEdit">Whether the record can be edited through the shared folder.</param>
/// <param name="CanShare">Whether the record can be reshared through the shared folder.</param>
internal sealed record KeeperFolder(string? SharedFolder, string? Folder, bool CanEdit, bool CanShare);
