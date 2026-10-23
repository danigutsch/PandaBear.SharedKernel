using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Formats;

/// <summary>Items read from one vault export and the problems found while reading it.</summary>
/// <param name="Export">Items read.</param>
/// <param name="Warnings">Lossy conversions or skipped data. Warnings name items and fields, never values.</param>
internal sealed record VaultReadResult(VaultExport Export, IReadOnlyList<string> Warnings)
{
    /// <summary>
    /// Attachment folders of a Bitwarden zip export whose files belong to no item read from it. A warning names each of
    /// them as well.
    /// </summary>
    public IReadOnlyList<UnassignedAttachmentFolder> UnassignedAttachmentFolders { get; init; } = [];
}
