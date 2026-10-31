using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PandaBear.VaultManager.Tool.Crypto;

namespace PandaBear.VaultManager.Tool.Formats.Bitwarden;

/// <summary>Reads and writes Bitwarden password-protected JSON exports.</summary>
internal static class PasswordProtectedExport
{
    private const int SaltLength = 16;

    /// <summary>Decrypts a password-protected export and returns the unencrypted Bitwarden JSON it contains.</summary>
    /// <param name="envelopeJson">Contents of the password-protected export file.</param>
    /// <param name="password">File password chosen when the export was created.</param>
    /// <returns>Unencrypted Bitwarden export JSON.</returns>
    /// <exception cref="InvalidDataException">The file is malformed, its settings are unsafe, or the password is wrong.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="envelopeJson"/> or <paramref name="password"/> is <see langword="null"/>.</exception>
    public static string Decrypt(string envelopeJson, string password)
    {
        ArgumentNullException.ThrowIfNull(envelopeJson);
        ArgumentNullException.ThrowIfNull(password);

        PasswordProtectedEnvelopeDto envelope = ReadEnvelope(envelopeJson);
        KdfConfig kdf = Kdf(envelope);
        using SymmetricKeys keys = BitwardenKeyDerivation.Derive(password, envelope.Salt!, kdf);

        // Validation entry first: a wrong password fails here before the large payload is processed.
        _ = EncString.Decrypt(envelope.EncKeyValidation!, keys);
        byte[] plaintext = EncString.Decrypt(envelope.Data!, keys);
        try
        {
            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    /// <summary>
    /// Checks the envelope of a password-protected export and its key derivation settings without a password, so a file
    /// that cannot be read is named before its password is asked.
    /// </summary>
    /// <param name="envelopeJson">Contents of the password-protected export file.</param>
    /// <exception cref="InvalidDataException">The file is malformed, or its settings are unsupported or outside the accepted ranges.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="envelopeJson"/> is <see langword="null"/>.</exception>
    public static void CheckEnvelope(string envelopeJson)
    {
        ArgumentNullException.ThrowIfNull(envelopeJson);

        Kdf(ReadEnvelope(envelopeJson)).Validate();
    }

    /// <summary>Encrypts unencrypted Bitwarden JSON into a password-protected export.</summary>
    /// <param name="plainJson">Unencrypted Bitwarden export JSON.</param>
    /// <param name="password">File password the importer will ask for.</param>
    /// <param name="kdf">Key derivation settings.</param>
    /// <returns>Password-protected export file contents.</returns>
    /// <exception cref="InvalidDataException">The key derivation settings are outside accepted ranges.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="plainJson"/>, <paramref name="password"/>, or <paramref name="kdf"/> is <see langword="null"/>.</exception>
    public static string Encrypt(string plainJson, string password, KdfConfig kdf)
    {
        ArgumentNullException.ThrowIfNull(plainJson);
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(kdf);

        string salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(SaltLength));
        using SymmetricKeys keys = BitwardenKeyDerivation.Derive(password, salt, kdf);
        byte[] plaintext = Encoding.UTF8.GetBytes(plainJson);
        try
        {
            var envelope = new PasswordProtectedEnvelopeDto
            {
                Encrypted = true,
                PasswordProtected = true,
                Salt = salt,
                KdfType = (int)kdf.Type,
                KdfIterations = kdf.Iterations,
                KdfMemory = kdf.Type == KdfType.Argon2id ? kdf.MemoryMiB : null,
                KdfParallelism = kdf.Type == KdfType.Argon2id ? kdf.Parallelism : null,
                EncKeyValidation = EncString.Encrypt(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()), keys),
                Data = EncString.Encrypt(plaintext, keys),
            };
            return JsonSerializer.Serialize(envelope, BitwardenJsonContext.Default.PasswordProtectedEnvelopeDto);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static KdfConfig Kdf(PasswordProtectedEnvelopeDto envelope) =>
        new((KdfType)envelope.KdfType, envelope.KdfIterations, envelope.KdfMemory, envelope.KdfParallelism);

    private static PasswordProtectedEnvelopeDto ReadEnvelope(string envelopeJson)
    {
        PasswordProtectedEnvelopeDto? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize(envelopeJson, BitwardenJsonContext.Default.PasswordProtectedEnvelopeDto);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Password-protected export is not valid JSON.", exception);
        }

        if (envelope is not { Encrypted: true, PasswordProtected: true }
            || string.IsNullOrEmpty(envelope.Salt)
            || string.IsNullOrEmpty(envelope.EncKeyValidation)
            || string.IsNullOrEmpty(envelope.Data))
        {
            throw new InvalidDataException("File is not a Bitwarden password-protected export.");
        }

        return envelope;
    }
}
