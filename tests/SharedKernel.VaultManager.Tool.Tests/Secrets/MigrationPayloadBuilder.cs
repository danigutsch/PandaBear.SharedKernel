using System.Text;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

/// <summary>Writes Google Authenticator migration payloads (protobuf) for decoder tests.</summary>
internal static class MigrationPayloadBuilder
{
    public static string Uri(params (byte[] Secret, string Name, string Issuer, int Algorithm, int Digits, int Type)[] entries)
    {
        using var payload = new MemoryStream();
        foreach (var entry in entries)
        {
            using var parameters = new MemoryStream();
            WriteBytes(parameters, 1, entry.Secret);
            WriteBytes(parameters, 2, Encoding.UTF8.GetBytes(entry.Name));
            WriteBytes(parameters, 3, Encoding.UTF8.GetBytes(entry.Issuer));
            WriteVarintField(parameters, 4, (ulong)entry.Algorithm);
            WriteVarintField(parameters, 5, (ulong)entry.Digits);
            WriteVarintField(parameters, 6, (ulong)entry.Type);
            WriteBytes(payload, 1, parameters.ToArray());
        }

        WriteVarintField(payload, 2, 1);
        return "otpauth-migration://offline?data=" + System.Uri.EscapeDataString(Convert.ToBase64String(payload.ToArray()));
    }

    private static void WriteBytes(MemoryStream stream, int field, byte[] value)
    {
        WriteVarint(stream, (ulong)((field << 3) | 2));
        WriteVarint(stream, (ulong)value.Length);
        stream.Write(value);
    }

    private static void WriteVarintField(MemoryStream stream, int field, ulong value)
    {
        WriteVarint(stream, (ulong)(field << 3));
        WriteVarint(stream, value);
    }

    private static void WriteVarint(MemoryStream stream, ulong value)
    {
        while (value >= 0x80)
        {
            stream.WriteByte((byte)(value | 0x80));
            value >>= 7;
        }

        stream.WriteByte((byte)value);
    }
}
