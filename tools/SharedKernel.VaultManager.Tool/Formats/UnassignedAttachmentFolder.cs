using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Formats;

/// <summary>
/// Attachment folder of a Bitwarden zip export whose files belong to no item read from it. The export names each folder
/// after its item and adds <c>_1</c>, <c>_2</c>, and so on for items with the same name, so a folder that matches no item
/// or several items cannot be assigned. The user uploads its files by hand. Holds names and paths, never file content.
/// </summary>
/// <param name="SourceFile">Name of the input file that holds the folder.</param>
/// <param name="FolderName">Folder name below <c>attachments/</c>.</param>
/// <param name="MatchingItemCount">Number of items that can have produced the folder: 0, or more than 1.</param>
/// <param name="Files">Files in the folder, with their paths inside <paramref name="SourceFile"/>.</param>
internal sealed record UnassignedAttachmentFolder(string SourceFile, string FolderName, int MatchingItemCount, IReadOnlyList<AttachmentRef> Files);
