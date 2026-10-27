namespace PandaBear.VaultManager.Tool.Tests.Secrets;

/// <summary>BIP173 bech32 encoder used to build age identity fixtures at runtime.</summary>
internal static class Bech32Reference
{
    private const string Charset = "qpzry9x8gf2tvdw0s3jn54khce6mua7l";

    /// <summary>Encodes <paramref name="data"/> as a bech32 string with the human-readable part <paramref name="hrp"/>.</summary>
    public static string Encode(string hrp, ReadOnlySpan<byte> data)
    {
        var values = new List<int>();
        int accumulator = 0;
        int bits = 0;
        foreach (byte value in data)
        {
            accumulator = (accumulator << 8) | value;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                values.Add((accumulator >> bits) & 31);
            }
        }

        if (bits > 0)
        {
            values.Add((accumulator << (5 - bits)) & 31);
        }

        var expanded = new List<int>();
        expanded.AddRange(hrp.Select(character => character >> 5));
        expanded.Add(0);
        expanded.AddRange(hrp.Select(character => character & 31));
        expanded.AddRange(values);
        expanded.AddRange([0, 0, 0, 0, 0, 0]);
        int polymod = Polymod(expanded) ^ 1;
        for (int index = 0; index < 6; index++)
        {
            values.Add((polymod >> (5 * (5 - index))) & 31);
        }

        return hrp + "1" + string.Concat(values.Select(value => Charset[value]));
    }

    private static int Polymod(List<int> values)
    {
        int[] generator = [0x3b6a57b2, 0x26508e6d, 0x1ea119fa, 0x3d4233dd, 0x2a1462b3];
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
