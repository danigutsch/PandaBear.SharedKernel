using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>
/// BIP39 mnemonic validation against the embedded English wordlist (bitcoin/bips, MIT), plus the Electrum seed version
/// check for phrases that fail the BIP39 checksum.
/// </summary>
internal static class Bip39
{
    private const string ResourceName = "PandaBear.VaultManager.Tool.Secrets.bip39-english.txt";
    private static readonly Lazy<Dictionary<string, int>> Words = new(LoadWords);

    /// <summary>Accepted mnemonic lengths in words.</summary>
    public static IReadOnlyList<int> Lengths { get; } = [24, 21, 18, 15, 12];

    /// <summary>Normalizes a word: NFKD, then lowercase.</summary>
    /// <param name="word">Word.</param>
    /// <returns>Normalized word.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "The BIP39 wordlist and Electrum normalization are lowercase.")]
    public static string NormalizeWord(string word)
    {
        ArgumentNullException.ThrowIfNull(word);
        return word.Normalize(NormalizationForm.FormKD).ToLowerInvariant();
    }

    /// <summary>Whether a normalized word is in the wordlist.</summary>
    /// <param name="word">Normalized word.</param>
    /// <returns><see langword="true"/> when listed.</returns>
    public static bool IsWord(string word)
    {
        return Words.Value.ContainsKey(word);
    }

    /// <summary>Whether words form a mnemonic with a valid checksum.</summary>
    /// <param name="words">Words, normalized or not.</param>
    /// <returns><see langword="true"/> when the checksum matches.</returns>
    public static bool IsValidMnemonic(IReadOnlyList<string> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        if (!Lengths.Contains(words.Count))
        {
            return false;
        }

        int totalBits = words.Count * 11;
        int checksumBits = totalBits / 33;
        int entropyBits = totalBits - checksumBits;
        var bits = new bool[totalBits];
        for (int wordIndex = 0; wordIndex < words.Count; wordIndex++)
        {
            if (!Words.Value.TryGetValue(NormalizeWord(words[wordIndex]), out int value))
            {
                return false;
            }

            for (int bit = 0; bit < 11; bit++)
            {
                bits[(wordIndex * 11) + bit] = ((value >> (10 - bit)) & 1) == 1;
            }
        }

        byte[] entropy = new byte[entropyBits / 8];
        for (int index = 0; index < entropyBits; index++)
        {
            if (bits[index])
            {
                entropy[index / 8] |= (byte)(0x80 >> (index % 8));
            }
        }

        byte[] hash = SHA256.HashData(entropy);
        CryptographicOperations.ZeroMemory(entropy);
        for (int index = 0; index < checksumBits; index++)
        {
            bool expected = ((hash[index / 8] >> (7 - (index % 8))) & 1) == 1;
            if (bits[entropyBits + index] != expected)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether a phrase is an Electrum seed: the hex HMAC-SHA512 keyed with <c>Seed version</c> over the normalized
    /// phrase starts with <c>01</c> (standard), <c>100</c> (segwit), or <c>101</c> (two-factor).
    /// </summary>
    /// <param name="words">Words.</param>
    /// <returns><see langword="true"/> when the version prefix matches.</returns>
    public static bool IsElectrumSeed(IReadOnlyList<string> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        string phrase = string.Join(' ', words.Select(NormalizeWord));
        byte[] message = Encoding.UTF8.GetBytes(phrase);
        byte[] mac = HMACSHA512.HashData("Seed version"u8, message);
        CryptographicOperations.ZeroMemory(message);
        string hex = Convert.ToHexStringLower(mac.AsSpan(0, 2));
        return hex.StartsWith("01", StringComparison.Ordinal)
            || hex.StartsWith("100", StringComparison.Ordinal)
            || hex.StartsWith("101", StringComparison.Ordinal);
    }

    private static Dictionary<string, int> LoadWords()
    {
        using Stream stream = typeof(Bip39).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The BIP39 wordlist resource is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var words = new Dictionary<string, int>(2048, StringComparer.Ordinal);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length > 0)
            {
                words.Add(line.Trim(), words.Count);
            }
        }

        return words.Count == 2048 ? words : throw new InvalidOperationException("The BIP39 wordlist must have 2048 words.");
    }
}
