namespace PandaBear.Vault.Formats;

/// <summary>Unencrypted export JSON written by an <see cref="IVaultExportWriter"/> and the import problems found while writing it.</summary>
/// <param name="Json">Unencrypted export JSON.</param>
/// <param name="Errors">Problems that make the target vault reject the whole import.</param>
/// <param name="Warnings">Adjustments made, or risks that do not block the import.</param>
public sealed record VaultWriteResult(string Json, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings);
