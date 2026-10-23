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
    public const string Alphanumeric = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    public const string LowerHex = "0123456789abcdef";
    public const string Base64Url = Alphanumeric + "-_";
    public const string Base64 = Alphanumeric + "+/";
    public const string Base32 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const string GitHubBase62 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

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

    public static string GitHubToken(string prefix, int seed)
    {
        string body = FromAlphabet(Alphanumeric, 30, seed);
        return prefix + "_" + body + Base62(Crc32Reference.Compute(body)).PadLeft(6, '0');
    }

    public static string NpmToken(int seed)
    {
        string body = FromAlphabet(Alphanumeric, 30, seed);
        return "npm_" + body + Base62(Crc32Reference.Compute(body)).PadLeft(6, '0');
    }

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

    public static string Base64UrlEncode(string text)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string SpacedDigitCode(int line, int column)
    {
        string digits = FromAlphabet("0123456789", 8, 100 + (line * 2) + column);
        return digits[..4] + " " + digits[4..];
    }

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

    public static string Jwt(int seed)
    {
        return Base64UrlEncode("{\"alg\":\"HS256\",\"typ\":\"JWT\"}") + "."
            + Base64UrlEncode("{\"sub\":\"fixture\",\"n\":" + seed.ToString(CultureInfo.InvariantCulture) + "}") + "."
            + FromAlphabet(Base64Url, 43, seed);
    }
}
