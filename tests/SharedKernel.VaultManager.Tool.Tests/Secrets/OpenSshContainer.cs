using System.Buffers.Binary;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

/// <summary>Builds unencrypted OpenSSH containers with arbitrary key types for converter edge cases.</summary>
internal static class OpenSshContainer
{
    public static string Unencrypted(string keyType, int privateFillerLength)
    {
        using var stream = new MemoryStream();
        stream.Write("openssh-key-v1\0"u8);
        WriteString(stream, "none"u8);
        WriteString(stream, "none"u8);
        WriteString(stream, []);
        WriteUInt32(stream, 1);
        using var publicBlob = new MemoryStream();
        WriteString(publicBlob, System.Text.Encoding.ASCII.GetBytes(keyType));
        WriteString(publicBlob, TestSecrets.Bytes(30, 32));
        WriteString(stream, publicBlob.ToArray());
        using var privateSection = new MemoryStream();
        WriteUInt32(privateSection, 0x01020304);
        WriteUInt32(privateSection, 0x01020304);
        WriteString(privateSection, System.Text.Encoding.ASCII.GetBytes(keyType));
        privateSection.Write(TestSecrets.Bytes(31, privateFillerLength));
        WriteString(stream, privateSection.ToArray());
        return PemFixtures.Wrap("OPENSSH PRIVATE KEY", Convert.ToBase64String(stream.ToArray()), 70);
    }

    private static void WriteString(MemoryStream stream, ReadOnlySpan<byte> value)
    {
        WriteUInt32(stream, (uint)value.Length);
        stream.Write(value);
    }

    private static void WriteUInt32(MemoryStream stream, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        stream.Write(buffer);
    }
}
