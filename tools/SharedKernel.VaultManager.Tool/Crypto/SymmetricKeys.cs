using System.Security.Cryptography;

namespace PandaBear.VaultManager.Tool.Crypto;

/// <summary>AES-256 encryption key and HMAC-SHA256 key pair. Disposing zeroes both keys.</summary>
internal sealed class SymmetricKeys(byte[] encryptionKey, byte[] macKey) : IDisposable
{
    /// <summary>AES-256 key used for encryption and decryption.</summary>
    public byte[] EncryptionKey { get; } = encryptionKey;

    /// <summary>HMAC-SHA256 key used to authenticate the ciphertext.</summary>
    public byte[] MacKey { get; } = macKey;

    /// <summary>Zeroes both keys.</summary>
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(EncryptionKey);
        CryptographicOperations.ZeroMemory(MacKey);
    }
}
