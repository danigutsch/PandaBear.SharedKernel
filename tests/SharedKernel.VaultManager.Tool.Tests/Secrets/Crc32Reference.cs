using System.Text;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

/// <summary>Bitwise CRC-32 (IEEE 802.3, reflected polynomial 0xEDB88320), kept separate from the production table code.</summary>
internal static class Crc32Reference
{
    public static uint Compute(string ascii)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte value in Encoding.ASCII.GetBytes(ascii))
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            }
        }

        return ~crc;
    }
}
