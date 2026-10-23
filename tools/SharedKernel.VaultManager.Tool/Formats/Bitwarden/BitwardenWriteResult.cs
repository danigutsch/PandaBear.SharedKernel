namespace PandaBear.VaultManager.Tool.Formats.Bitwarden;

/// <summary>Unencrypted Bitwarden export JSON and the import problems found while writing it.</summary>
/// <param name="Json">Bitwarden export JSON.</param>
/// <param name="Errors">Problems that make Bitwarden or Vaultwarden reject the whole import.</param>
/// <param name="Warnings">Adjustments made, or risks that do not block the import.</param>
internal sealed record BitwardenWriteResult(string Json, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings);
