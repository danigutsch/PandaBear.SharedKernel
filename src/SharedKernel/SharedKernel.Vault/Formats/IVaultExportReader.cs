namespace SharedKernel.Vault.Formats;

/// <summary>Reads vault export files into provider-neutral items for <see cref="Pipeline.VaultPipeline"/>.</summary>
public interface IVaultExportReader
{
    /// <summary>Reads a vault export.</summary>
    /// <param name="path">Export file.</param>
    /// <param name="passwordProvider">Returns the file password for a password-protected export, given its file name.</param>
    /// <returns>Items and warnings. Warnings name items and fields, never values.</returns>
    /// <exception cref="InvalidDataException">
    /// The file is too large, unsupported, malformed, or cannot be decrypted. The message names no vault value.
    /// </exception>
    /// <exception cref="IOException">The file does not exist, is a directory, or cannot be read.</exception>
    VaultReadResult Read(string path, Func<string, string> passwordProvider);
}
