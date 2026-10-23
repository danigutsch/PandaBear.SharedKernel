namespace PandaBear.VaultManager.Tool.Pipeline;

/// <summary>
/// File attached to an item in a source vault. Bitwarden JSON imports cannot carry files, so the user uploads it after
/// the import.
/// </summary>
/// <param name="ItemName">Name of the output item the file belongs to.</param>
/// <param name="FileName">Attachment file name.</param>
/// <param name="SourceFile">Name of the input file that listed the attachment.</param>
/// <param name="ArchivePath">
/// Path of the file inside <paramref name="SourceFile"/>, or <see langword="null"/> when the export did not include the
/// file and it has to be downloaded from the source vault.
/// </param>
internal sealed record ManualAttachment(string ItemName, string FileName, string SourceFile, string? ArchivePath);
