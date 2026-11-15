using System.Text;

namespace PandaBear.Vault.Formats;

/// <summary>Unencrypted export JSON written by an <see cref="IVaultExportWriter"/> and the import problems found while writing it.</summary>
/// <param name="Json">Unencrypted export JSON.</param>
/// <param name="Errors">Problems that make the target vault reject the whole import.</param>
/// <param name="Warnings">Adjustments made, or risks that do not block the import.</param>
public sealed record VaultWriteResult(string Json, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings)
{
    /// <summary>Prints the problem counts only, so the unencrypted export never reaches logs or messages through <c>ToString</c>.</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Errors = ").Append(Errors.Count).Append(", Warnings = ").Append(Warnings.Count);
        return true;
    }
}
