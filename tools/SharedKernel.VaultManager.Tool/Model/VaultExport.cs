namespace PandaBear.VaultManager.Tool.Model;

/// <summary>Items read from, or written to, one vault export.</summary>
/// <param name="Items">Vault items.</param>
internal sealed record VaultExport(IReadOnlyList<VaultItem> Items)
{
    /// <summary>
    /// Folder paths the export declares, with <c>/</c> separators, in the order they were read, including folders that
    /// hold no item. A writer also creates the folders of items and the parents of every folder.
    /// </summary>
    public IReadOnlyList<string> Folders { get; init; } = [];
}
