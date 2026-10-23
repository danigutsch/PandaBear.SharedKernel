namespace PandaBear.VaultManager.Tool.Pipeline;

/// <summary>Scanned items and every warning raised while reading and scanning them.</summary>
/// <param name="InputPaths">Input files the items were read from, in order. An output may not be one of them.</param>
/// <param name="Items">Items in input order.</param>
/// <param name="Warnings">
/// Read warnings, then attachment scanning warnings. Warnings name items, fields, and files, never values.
/// </param>
internal sealed record ScannedVault(IReadOnlyList<string> InputPaths, IReadOnlyList<ScannedItem> Items, IReadOnlyList<string> Warnings);
