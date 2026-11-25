using SharedKernel.Vault.Model;

namespace SharedKernel.Vault.Formats;

/// <summary>Writes provider-neutral items as an export file that the target vault imports.</summary>
public interface IVaultExportWriter
{
    /// <summary>Writes items as unencrypted export JSON.</summary>
    /// <param name="vault">Items and folders to write.</param>
    /// <returns>The export JSON and the problems that would affect the import. Problems never quote a vault value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="vault"/> is <see langword="null"/>.</exception>
    VaultWriteResult Write(VaultExport vault);

    /// <summary>Encrypts unencrypted export JSON into a password-protected export.</summary>
    /// <param name="json">Unencrypted export JSON from <see cref="Write"/>.</param>
    /// <param name="password">File password the importer will ask for.</param>
    /// <returns>Password-protected export file contents.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="json"/> or <paramref name="password"/> is <see langword="null"/>.</exception>
    string Encrypt(string json, string password);
}
