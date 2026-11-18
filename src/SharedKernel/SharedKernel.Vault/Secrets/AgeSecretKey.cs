using PandaBear.Encodings;

namespace PandaBear.Vault.Secrets;

/// <summary>Recognises an age identity, <c>AGE-SECRET-KEY-1...</c>, by its Bech32 checksum and its 32-byte key.</summary>
internal static class AgeSecretKey
{
    private const string HumanReadablePart = "age-secret-key-";
    private const int KeyBytes = 32;

    /// <summary>Whether text is an age identity: valid Bech32 in one letter case, the age prefix, and a 32-byte key.</summary>
    /// <param name="value">Candidate.</param>
    /// <returns><see langword="true"/> when the text is an age identity.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    public static bool IsValid(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Bech32.TryDecode(value, maxLength: null, out var humanReadablePart, out var data)
            && humanReadablePart == HumanReadablePart
            && Bech32.TryConvertToBytes(data, out var key)
            && key.Length == KeyBytes;
    }
}
