namespace PandaBear.VaultManager.Tool.Pipeline;

/// <summary>Scanned items and every warning raised while reading and scanning them.</summary>
/// <param name="Items">Items in input order.</param>
/// <param name="Warnings">
/// Read warnings, then attachment scanning warnings. Warnings name items, fields, and files, never values.
/// </param>
internal sealed record ScannedVault(IReadOnlyList<ScannedItem> Items, IReadOnlyList<string> Warnings);
