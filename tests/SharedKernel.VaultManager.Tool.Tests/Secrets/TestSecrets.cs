using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

/// <summary>
/// Builds secret-shaped fixtures at test runtime, so the public repository never holds literal checksum-valid tokens.
/// </summary>
internal static class TestSecrets
{
    /// <summary>Letters and digits.</summary>
    public const string Alphanumeric = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    /// <summary>Lowercase hexadecimal digits.</summary>
    public const string LowerHex = "0123456789abcdef";
    /// <summary>Alphabet of URL-safe Base64.</summary>
    public const string Base64Url = Alphanumeric + "-_";
    /// <summary>Alphabet of standard Base64 without padding.</summary>
    public const string Base64 = Alphanumeric + "+/";
    /// <summary>Alphabet of RFC 4648 Base32.</summary>
    public const string Base32 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const string GitHubBase62 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    /// <summary>Returns <paramref name="count"/> deterministic bytes derived from <paramref name="seed"/> by chained SHA-256 hashes.</summary>
    public static byte[] Bytes(int seed, int count)
    {
        var output = new byte[count];
        int written = 0;
        for (int block = 0; written < count; block++)
        {
            byte[] hash = SHA256.HashData(Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"fixture-{seed}-{block}")));
            int take = Math.Min(hash.Length, count - written);
            hash.AsSpan(0, take).CopyTo(output.AsSpan(written));
            written += take;
        }

        return output;
    }

    /// <summary>Returns a deterministic string of <paramref name="length"/> characters of <paramref name="alphabet"/> chosen from <paramref name="seed"/>.</summary>
    public static string FromAlphabet(string alphabet, int length, int seed)
    {
        byte[] bytes = Bytes(seed, length);
        var builder = new StringBuilder(length);
        foreach (byte value in bytes)
        {
            builder.Append(alphabet[value % alphabet.Length]);
        }

        return builder.ToString();
    }

    /// <summary>Returns a GitHub token with the given prefix whose last six characters are the Base62 CRC-32 of its body.</summary>
    public static string GitHubToken(string prefix, int seed)
    {
        string body = FromAlphabet(Alphanumeric, 30, seed);
        return prefix + "_" + body + Base62(Crc32Reference.Compute(body)).PadLeft(6, '0');
    }

    /// <summary>Returns an npm token whose last six characters are the Base62 CRC-32 of its body.</summary>
    public static string NpmToken(int seed)
    {
        string body = FromAlphabet(Alphanumeric, 30, seed);
        return "npm_" + body + Base62(Crc32Reference.Compute(body)).PadLeft(6, '0');
    }

    /// <summary>Returns <paramref name="value"/> in Base62 using the GitHub alphabet.</summary>
    public static string Base62(uint value)
    {
        var builder = new StringBuilder();
        do
        {
            builder.Insert(0, GitHubBase62[(int)(value % 62)]);
            value /= 62;
        }
        while (value > 0);

        return builder.ToString();
    }

    /// <summary>Returns a card number of <paramref name="length"/> digits that starts with <paramref name="prefix"/> and ends with a valid Luhn check digit.</summary>
    public static string CardNumber(string prefix, int length)
    {
        var digits = new StringBuilder(prefix);
        byte[] filler = Bytes(prefix.Length + length, length);
        for (int index = digits.Length; index < length - 1; index++)
        {
            digits.Append((char)('0' + (filler[index] % 10)));
        }

        for (char check = '0'; check <= '9'; check++)
        {
            string candidate = digits.ToString() + check;
            if (LuhnValid(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("No Luhn check digit.");
    }

    /// <summary>Returns whether <paramref name="digits"/> pass the Luhn check.</summary>
    public static bool LuhnValid(string digits)
    {
        int sum = 0;
        bool doubleIt = false;
        for (int index = digits.Length - 1; index >= 0; index--)
        {
            int digit = digits[index] - '0';
            if (doubleIt)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
            doubleIt = !doubleIt;
        }

        return sum % 10 == 0;
    }

    /// <summary>Returns an IBAN for <paramref name="country"/> and <paramref name="bban"/> with the computed mod-97 check digits.</summary>
    public static string Iban(string country, string bban)
    {
        string rearranged = bban + country + "00";
        var numeric = new StringBuilder();
        foreach (char character in rearranged)
        {
            numeric.Append(char.IsAsciiDigit(character)
                ? character.ToString()
                : (character - 'A' + 10).ToString(CultureInfo.InvariantCulture));
        }

        BigInteger remainder = BigInteger.Parse(numeric.ToString(), CultureInfo.InvariantCulture) % 97;
        int check = 98 - (int)remainder;
        return country + check.ToString("00", CultureInfo.InvariantCulture) + bban;
    }

    /// <summary>Encodes <paramref name="data"/> as unpadded Base32.</summary>
    public static string Base32Encode(ReadOnlySpan<byte> data)
    {
        var builder = new StringBuilder();
        int buffer = 0;
        int bits = 0;
        foreach (byte value in data)
        {
            buffer = (buffer << 8) | value;
            bits += 8;
            while (bits >= 5)
            {
                builder.Append(Base32[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        if (bits > 0)
        {
            builder.Append(Base32[(buffer << (5 - bits)) & 31]);
        }

        return builder.ToString();
    }

    /// <summary>Encodes the UTF-8 bytes of <paramref name="text"/> as unpadded URL-safe Base64.</summary>
    public static string Base64UrlEncode(string text)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>Returns an eight-digit recovery code with a space after the fourth digit, derived from the line and column.</summary>
    public static string SpacedDigitCode(int line, int column)
    {
        string digits = FromAlphabet("0123456789", 8, 100 + (line * 2) + column);
        return digits[..4] + " " + digits[4..];
    }

    /// <summary>Returns <paramref name="value"/> with every character in lowercase invariant form.</summary>
    public static string Lower(string value)
    {
        return string.Create(value.Length, value, static (span, source) =>
        {
            for (int index = 0; index < source.Length; index++)
            {
                span[index] = char.ToLowerInvariant(source[index]);
            }
        });
    }

    /// <summary>Returns a JWT-shaped string with a fixed header, a payload carrying <paramref name="seed"/>, and a filler signature.</summary>
    public static string Jwt(int seed)
    {
        return Base64UrlEncode("{\"alg\":\"HS256\",\"typ\":\"JWT\"}") + "."
            + Base64UrlEncode("{\"sub\":\"fixture\",\"n\":" + seed.ToString(CultureInfo.InvariantCulture) + "}") + "."
            + FromAlphabet(Base64Url, 43, seed);
    }
}
