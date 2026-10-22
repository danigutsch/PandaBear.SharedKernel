namespace PandaBear.VaultManager.Tool.Model;

/// <summary>File attached to a vault item in the source vault.</summary>
/// <param name="FileName">Attachment file name.</param>
/// <param name="Size">Size in bytes, when known.</param>
/// <param name="SourcePath">Path inside the source export archive, when the file is included.</param>
internal sealed record AttachmentRef(string FileName, long? Size, string? SourcePath);
