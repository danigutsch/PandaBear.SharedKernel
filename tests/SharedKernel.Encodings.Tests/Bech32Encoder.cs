namespace SharedKernel.Encodings.Tests;

/// <summary>Encodes Bech32 strings with a valid checksum so tests can build recipients with chosen human-readable parts.</summary>
internal static class Bech32Encoder
{
    private const string Charset = "qpzry9x8gf2tvdw0s3jn54khce6mua7l";
    private static readonly int[] Generator = [0x3b6a57b2, 0x26508e6d, 0x1ea119fa, 0x3d4233dd, 0x2a1462b3];

    /// <summary>Encodes <paramref name="payload"/> bytes under <paramref name="hrp"/>.</summary>
    public static string Encode(string hrp, byte[] payload)
    {
        List<int> data = [];
        var accumulator = 0;
        var bits = 0;
        foreach (var value in payload)
        {
            accumulator = (accumulator << 8) | value;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                data.Add((accumulator >> bits) & 31);
            }

            accumulator &= (1 << bits) - 1;
        }

        if (bits > 0)
        {
            data.Add((accumulator << (5 - bits)) & 31);
        }

        List<int> values = [.. hrp.Select(static c => c >> 5), 0, .. hrp.Select(static c => c & 31), .. data, 0, 0, 0, 0, 0, 0];
        var polymod = Polymod(values) ^ 1;
        for (var index = 0; index < 6; index++)
        {
            data.Add((polymod >> (5 * (5 - index))) & 31);
        }

        return hrp + "1" + new string([.. data.Select(static v => Charset[v])]);
    }

    private static int Polymod(List<int> values)
    {
        var checksum = 1;
        foreach (var value in values)
        {
            var top = checksum >> 25;
            checksum = ((checksum & 0x1ffffff) << 5) ^ value;
            for (var index = 0; index < 5; index++)
            {
                if (((top >> index) & 1) != 0)
                {
                    checksum ^= Generator[index];
                }
            }
        }

        return checksum;
    }
}
