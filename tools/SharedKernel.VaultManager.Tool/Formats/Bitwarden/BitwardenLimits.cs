using System.Text;

namespace PandaBear.VaultManager.Tool.Formats.Bitwarden;

/// <summary>
/// Bitwarden server limits on encrypted field length. Vaultwarden only checks notes, so targeting these limits keeps
/// an export importable by both.
/// </summary>
internal static class BitwardenLimits
{
    /// <summary>Encrypted length limit of item names, custom field names, and TOTP values.</summary>
    public const int ShortEncryptedLength = 1_000;

    /// <summary>Encrypted length limit of passwords, custom field values, and SSH keys.</summary>
    public const int ValueEncryptedLength = 5_000;

    /// <summary>Encrypted length limit of notes.</summary>
    public const int NotesEncryptedLength = 10_000;

    /// <summary>Password history entries kept by Bitwarden clients on import.</summary>
    public const int PasswordHistoryEntries = 5;

    /// <summary>
    /// Import request size limit of Vaultwarden, in bytes: Rocket's <c>20.megabytes()</c>, where a megabyte is 1,000,000
    /// bytes. The request holds every value encrypted, so it is larger than the export file.
    /// </summary>
    public const long VaultwardenImportBytes = 20_000_000;

    /// <summary>Length of a value once encrypted as a Bitwarden type 2 encrypted string.</summary>
    /// <param name="value">Plaintext value.</param>
    /// <returns>Encrypted string length in characters.</returns>
    public static int EncryptedLength(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        int plaintextBytes = Encoding.UTF8.GetByteCount(value);
        int ciphertextBytes = ((plaintextBytes / 16) + 1) * 16;
        // "2." + base64(16-byte IV) + "|" + base64(ciphertext) + "|" + base64(32-byte MAC)
        return 2 + 24 + 1 + Base64Length(ciphertextBytes) + 1 + 44;
    }

    /// <summary>Whether a value fits a limit once encrypted.</summary>
    /// <param name="value">Plaintext value, or <see langword="null"/>.</param>
    /// <param name="encryptedLimit">One of the encrypted length limits.</param>
    /// <returns><see langword="true"/> when the value is absent or fits.</returns>
    public static bool Fits(string? value, int encryptedLimit)
    {
        return value is null || EncryptedLength(value) <= encryptedLimit;
    }

    private static int Base64Length(int bytes)
    {
        return (bytes + 2) / 3 * 4;
    }
}
