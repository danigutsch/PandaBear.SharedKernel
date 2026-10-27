using System.Security.Cryptography;
using System.Text;
using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

/// <summary>Builds BIP39 mnemonics with an independent checksum computation.</summary>
internal static class Bip39Reference
{
    private static readonly Lazy<string[]> Words = new(LoadWords);

    /// <summary>Returns the BIP39 mnemonic for <paramref name="entropy"/>, with the checksum bits appended.</summary>
    public static string Mnemonic(byte[] entropy)
    {
        byte[] hash = SHA256.HashData(entropy);
        int entropyBits = entropy.Length * 8;
        int checksumBits = entropyBits / 32;
        var bits = new StringBuilder();
        foreach (byte value in entropy)
        {
            bits.Append(Convert.ToString(value, 2).PadLeft(8, '0'));
        }

        bits.Append(Convert.ToString(hash[0], 2).PadLeft(8, '0').AsSpan(0, checksumBits));
        var words = new List<string>();
        for (int offset = 0; offset < bits.Length; offset += 11)
        {
            words.Add(Words.Value[Convert.ToInt32(bits.ToString(offset, 11), 2)]);
        }

        return string.Join(' ', words);
    }

    /// <summary>Same words with the last word replaced so that neither the BIP39 checksum nor the Electrum version prefix matches.</summary>
    public static string InvalidVariant(string mnemonic)
    {
        string[] words = mnemonic.Split(' ');
        for (int index = 0; index < Words.Value.Length; index++)
        {
            words[^1] = Words.Value[index];
            string candidate = string.Join(' ', words);
            if (!ChecksumValid(words) && !LooksLikeElectrum(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("No invalid variant.");
    }

    private static bool ChecksumValid(string[] words)
    {
        var bits = new StringBuilder();
        foreach (string word in words)
        {
            bits.Append(Convert.ToString(Array.IndexOf(Words.Value, word), 2).PadLeft(11, '0'));
        }

        int checksumBits = bits.Length / 33;
        int entropyBits = bits.Length - checksumBits;
        byte[] entropy = new byte[entropyBits / 8];
        for (int index = 0; index < entropy.Length; index++)
        {
            entropy[index] = Convert.ToByte(bits.ToString(index * 8, 8), 2);
        }

        string expected = Convert.ToString(SHA256.HashData(entropy)[0], 2).PadLeft(8, '0')[..checksumBits];
        return bits.ToString(entropyBits, checksumBits) == expected;
    }

    private static bool LooksLikeElectrum(string phrase)
    {
        string hex = Convert.ToHexStringLower(HMACSHA512.HashData("Seed version"u8, Encoding.UTF8.GetBytes(phrase)));
        return hex.StartsWith("01", StringComparison.Ordinal) || hex.StartsWith("100", StringComparison.Ordinal) || hex.StartsWith("101", StringComparison.Ordinal);
    }

    private static string[] LoadWords()
    {
        using Stream stream = typeof(SecretScanner).Assembly.GetManifestResourceStream("PandaBear.VaultManager.Tool.Secrets.bip39-english.txt")
            ?? throw new InvalidOperationException("Wordlist resource missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>Returns a twelve-word phrase that has an invalid BIP39 checksum but whose HMAC-SHA512 seed-version hash starts with <c>01</c>, so it reads as an Electrum seed.</summary>
    public static string ElectrumPhrase()
    {
        string[] words = Mnemonic(TestSecrets.Bytes(500, 16)).Split(' ');
        for (int seed = 0; seed < 100_000; seed++)
        {
            words[0] = Mnemonic(TestSecrets.Bytes(seed, 16)).Split(' ')[0];
            words[1] = Mnemonic(TestSecrets.Bytes(seed + 1, 16)).Split(' ')[1];
            string phrase = string.Join(' ', words);
            string hex = Convert.ToHexStringLower(HMACSHA512.HashData("Seed version"u8, Encoding.UTF8.GetBytes(phrase)));
            if (hex.StartsWith("01", StringComparison.Ordinal) && !ChecksumValid(words))
            {
                return phrase;
            }
        }

        throw new InvalidOperationException("No Electrum fixture phrase.");
    }
}
