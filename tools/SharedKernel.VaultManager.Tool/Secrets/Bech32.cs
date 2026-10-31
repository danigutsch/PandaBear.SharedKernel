namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>BIP173 bech32 checksum validation, used for age identities.</summary>
internal static class Bech32
{
    private const string Charset = "QPZRY9X8GF2TVDW0S3JN54KHCE6MUA7L";
    private const string AgeHrp = "age-secret-key-";
    private const string AgeHrpUpper = "AGE-SECRET-KEY-";

    /// <summary>Whether text is an age identity with a valid bech32 checksum.</summary>
    /// <param name="value">Candidate, in either case.</param>
    /// <returns><see langword="true"/> when the checksum is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static bool IsValidAgeSecretKey(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string upper = value.ToUpperInvariant();
        if (!upper.StartsWith(AgeHrpUpper + "1", StringComparison.Ordinal) || upper.Length != AgeHrp.Length + 1 + 58)
        {
            return false;
        }

        var values = new List<int>(AgeHrp.Length * 2 + 1 + 58);
        foreach (char character in AgeHrp)
        {
            values.Add(character >> 5);
        }

        values.Add(0);
        foreach (char character in AgeHrp)
        {
            values.Add(character & 31);
        }

        foreach (char character in upper.AsSpan(AgeHrp.Length + 1))
        {
            int digit = Charset.IndexOf(character, StringComparison.Ordinal);
            if (digit < 0)
            {
                return false;
            }

            values.Add(digit);
        }

        return Polymod(values) == 1;
    }

    private static int Polymod(List<int> values)
    {
        ReadOnlySpan<int> generator = [0x3b6a57b2, 0x26508e6d, 0x1ea119fa, 0x3d4233dd, 0x2a1462b3];
        int checksum = 1;
        foreach (int value in values)
        {
            int top = checksum >> 25;
            checksum = ((checksum & 0x1ffffff) << 5) ^ value;
            for (int index = 0; index < 5; index++)
            {
                if (((top >> index) & 1) != 0)
                {
                    checksum ^= generator[index];
                }
            }
        }

        return checksum;
    }
}
