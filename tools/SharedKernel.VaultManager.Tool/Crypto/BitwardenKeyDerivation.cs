using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace PandaBear.VaultManager.Tool.Crypto;

/// <summary>Derives Bitwarden export keys from a file password.</summary>
internal static class BitwardenKeyDerivation
{
    private const int KeyLength = 32;

    /// <summary>Derives and stretches the export key. Validates <paramref name="kdf"/> first.</summary>
    public static SymmetricKeys Derive(string password, string salt, KdfConfig kdf)
    {
        kdf.Validate();

        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        // Bitwarden uses the UTF-8 bytes of the salt string as stored in the file; it is never base64-decoded.
        byte[] saltBytes = Encoding.UTF8.GetBytes(salt);
        byte[] masterKey = kdf.Type == KdfType.Argon2id
            ? DeriveArgon2id(passwordBytes, saltBytes, kdf)
            : Rfc2898DeriveBytes.Pbkdf2(passwordBytes, saltBytes, kdf.Iterations, HashAlgorithmName.SHA256, KeyLength);

        try
        {
            var encryptionKey = new byte[KeyLength];
            var macKey = new byte[KeyLength];
            // Bitwarden stretches with HKDF-Expand only; the derived key is used directly as the PRK.
            HKDF.Expand(HashAlgorithmName.SHA256, masterKey, encryptionKey, "enc"u8);
            HKDF.Expand(HashAlgorithmName.SHA256, masterKey, macKey, "mac"u8);
            return new SymmetricKeys(encryptionKey, macKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(masterKey);
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    private static byte[] DeriveArgon2id(byte[] password, byte[] salt, KdfConfig kdf)
    {
        using var argon2 = new Argon2id(password)
        {
            Salt = SHA256.HashData(salt),
            Iterations = kdf.Iterations,
            MemorySize = kdf.MemoryMiB!.Value * 1024,
            DegreeOfParallelism = kdf.Parallelism!.Value,
        };
        return argon2.GetBytes(KeyLength);
    }
}
