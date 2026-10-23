namespace PandaBear.VaultManager.Tool.Pipeline;

/// <summary>Items read from every input file and the warnings raised while reading them.</summary>
/// <param name="Items">Items in input order.</param>
/// <param name="Warnings">
/// Lossy conversions or skipped data, each prefixed with the input file name. Warnings name items and fields, never
/// values.
/// </param>
internal sealed record LoadedVault(IReadOnlyList<SourceItem> Items, IReadOnlyList<string> Warnings);
