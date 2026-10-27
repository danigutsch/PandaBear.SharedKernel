using System.Security.Cryptography;

namespace PandaBear.VaultManager.Tool.Crypto;

/// <summary>AES-256 encryption key and HMAC-SHA256 key pair. Disposing zeroes both keys.</summary>
internal sealed class SymmetricKeys(byte[] encryptionKey, byte[] macKey) : IDisposable
{
    public byte[] EncryptionKey { get; } = encryptionKey;

    public byte[] MacKey { get; } = macKey;

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(EncryptionKey);
        CryptographicOperations.ZeroMemory(MacKey);
    }
}
