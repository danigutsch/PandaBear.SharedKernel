namespace PandaBear.VaultManager.Tool.Attachments;

/// <summary>File entry of a validated archive.</summary>
/// <param name="Path">Entry path with <c>/</c> separators.</param>
/// <param name="Size">Expanded size in bytes, measured while validating.</param>
/// <param name="Kind">Content kind.</param>
internal sealed record ArchiveEntryInfo(string Path, long Size, AttachmentKind Kind);
