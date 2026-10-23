namespace PandaBear.VaultManager.Tool.Formats.Keeper;

/// <summary>File attached to a Keeper record in a Commander export.</summary>
/// <param name="FileUid">File identifier; Commander zips store the file at <c>files/&lt;FileUid&gt;</c>.</param>
/// <param name="Name">File name.</param>
internal sealed record KeeperAttachment(string? FileUid, string Name);
