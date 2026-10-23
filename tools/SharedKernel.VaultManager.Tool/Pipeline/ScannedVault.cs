using PandaBear.VaultManager.Tool.Formats;

namespace PandaBear.VaultManager.Tool.Pipeline;

/// <summary>Scanned items and every warning raised while reading and scanning them.</summary>
/// <param name="InputPaths">Input files the items were read from, in order. An output may not be one of them.</param>
/// <param name="Items">Items in input order.</param>
/// <param name="Warnings">
/// Read warnings, then attachment scanning warnings. Warnings name items, fields, and files, never values.
/// </param>
internal sealed record ScannedVault(IReadOnlyList<string> InputPaths, IReadOnlyList<ScannedItem> Items, IReadOnlyList<string> Warnings)
{
    /// <summary>Attachment folders of Bitwarden zip inputs whose files belong to no item, as loaded.</summary>
    public IReadOnlyList<UnassignedAttachmentFolder> UnassignedAttachmentFolders { get; init; } = [];

    /// <summary>Distinct folders that the inputs declare, as loaded.</summary>
    public IReadOnlyList<string> Folders { get; init; } = [];
}
