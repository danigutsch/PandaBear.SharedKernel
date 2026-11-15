using PandaBear.Vault.Bitwarden.Crypto;
using PandaBear.Vault.Formats;
using PandaBear.Vault.Model;

namespace PandaBear.Vault.Bitwarden;

/// <summary>
/// Writes unencrypted Bitwarden JSON exports that Bitwarden and Vaultwarden can import, and encrypts them as
/// password-protected Bitwarden exports with PBKDF2-SHA256 and 600,000 iterations.
/// </summary>
public sealed class BitwardenExportWriter : IVaultExportWriter
{
    /// <summary>Writer shared by every caller; it holds no state.</summary>
    public static BitwardenExportWriter Instance { get; } = new();

    /// <inheritdoc/>
    public VaultWriteResult Write(VaultExport vault) => BitwardenJsonWriter.Write(vault);

    /// <inheritdoc/>
    public string Encrypt(string json, string password) => PasswordProtectedExport.Encrypt(json, password, KdfConfig.DefaultPbkdf2);
}
