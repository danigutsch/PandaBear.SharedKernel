namespace PandaBear.Vault.Attachments;

/// <summary>File entry of a validated archive.</summary>
/// <param name="Path">Entry path with <c>/</c> separators.</param>
/// <param name="Size">Expanded size in bytes, measured while validating.</param>
/// <param name="Kind">Content kind.</param>
public sealed record ArchiveEntryInfo(string Path, long Size, AttachmentKind Kind);
