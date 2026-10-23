using PandaBear.VaultManager.Tool.Formats;

namespace PandaBear.VaultManager.Tool.Pipeline;

/// <summary>Items read from every input file and the warnings raised while reading them.</summary>
/// <param name="InputPaths">Input files, in the order they were read.</param>
/// <param name="Items">Items in input order.</param>
/// <param name="Warnings">
/// Lossy conversions or skipped data, each prefixed with the input file name. Warnings name items and fields, never
/// values.
/// </param>
internal sealed record LoadedVault(IReadOnlyList<string> InputPaths, IReadOnlyList<SourceItem> Items, IReadOnlyList<string> Warnings)
{
    /// <summary>
    /// Attachment folders of Bitwarden zip inputs whose files belong to no item, in input order. The user uploads their
    /// files by hand.
    /// </summary>
    public IReadOnlyList<UnassignedAttachmentFolder> UnassignedAttachmentFolders { get; init; } = [];

    /// <summary>Distinct folders that the inputs declare, in input order (<see cref="Model.VaultExport.Folders"/>).</summary>
    public IReadOnlyList<string> Folders { get; init; } = [];
}
