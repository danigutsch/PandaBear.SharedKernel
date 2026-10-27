using System.Security.Cryptography;

namespace PandaBear.VaultManager.Tool.Crypto;

/// <summary>Bitwarden type 2 encrypted string: <c>2.iv|ciphertext|mac</c>, AES-256-CBC with HMAC-SHA256.</summary>
internal static class EncString
{
    private const string Prefix = "2.";
    private const int IvLength = 16;
    private const int MacLength = 32;

    public static string Encrypt(ReadOnlySpan<byte> plaintext, SymmetricKeys keys)
    {
        byte[] iv = RandomNumberGenerator.GetBytes(IvLength);
        using var aes = Aes.Create();
        aes.Key = keys.EncryptionKey;
        byte[] ciphertext = aes.EncryptCbc(plaintext, iv, PaddingMode.PKCS7);
        byte[] mac = ComputeMac(keys.MacKey, iv, ciphertext);
        return $"{Prefix}{Convert.ToBase64String(iv)}|{Convert.ToBase64String(ciphertext)}|{Convert.ToBase64String(mac)}";
    }

    /// <exception cref="InvalidDataException">The value is malformed, the password is wrong, or the data was altered.</exception>
    public static byte[] Decrypt(string encString, SymmetricKeys keys)
    {
        if (!encString.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Encrypted value is not a Bitwarden type 2 encrypted string.");
        }

        string[] parts = encString[Prefix.Length..].Split('|');
        if (parts.Length != 3)
        {
            throw new InvalidDataException("Encrypted value must have IV, ciphertext, and MAC parts.");
        }

        byte[] iv;
        byte[] ciphertext;
        byte[] mac;
        try
        {
            iv = Convert.FromBase64String(parts[0]);
            ciphertext = Convert.FromBase64String(parts[1]);
            mac = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("Encrypted value contains invalid base64.", exception);
        }

        if (iv.Length != IvLength || mac.Length != MacLength)
        {
            throw new InvalidDataException("Encrypted value has an invalid IV or MAC length.");
        }

        byte[] expectedMac = ComputeMac(keys.MacKey, iv, ciphertext);
        if (!CryptographicOperations.FixedTimeEquals(mac, expectedMac))
        {
            throw new InvalidDataException("The password is wrong or the file was altered.");
        }

        using var aes = Aes.Create();
        aes.Key = keys.EncryptionKey;
        try
        {
            return aes.DecryptCbc(ciphertext, iv, PaddingMode.PKCS7);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidDataException("Encrypted value could not be decrypted.", exception);
        }
    }

    private static byte[] ComputeMac(byte[] macKey, byte[] iv, byte[] ciphertext)
    {
        var data = new byte[iv.Length + ciphertext.Length];
        iv.CopyTo(data, 0);
        ciphertext.CopyTo(data, iv.Length);
        return HMACSHA256.HashData(macKey, data);
    }
}
