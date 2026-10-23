using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Pipeline;

/// <summary>Item read from an input file, with what later stages need to find it again.</summary>
/// <param name="Item">Item as read, with its SSH key completed by <see cref="SshKeyNormalizer"/>.</param>
/// <param name="OriginKey">
/// Origin key of the item as read, from <see cref="Matching.OriginKeys"/>. Finding, field, and match decisions name the
/// item by this key, so placement changes never change it.
/// </param>
/// <param name="InputPath">
/// Path of the input file. Attachments with an <see cref="AttachmentRef.SourcePath"/> are entries of this archive.
/// </param>
internal sealed record SourceItem(VaultItem Item, string OriginKey, string InputPath);
