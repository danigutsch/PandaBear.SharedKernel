using System.Buffers.Binary;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

/// <summary>Splits an SSH wire-format blob into its length-prefixed strings.</summary>
internal static class SshWireStrings
{
    public static List<byte[]> Read(byte[] blob)
    {
        var strings = new List<byte[]>();
        int offset = 0;
        while (offset < blob.Length)
        {
            int length = (int)BinaryPrimitives.ReadUInt32BigEndian(blob.AsSpan(offset));
            strings.Add(blob.AsSpan(offset + 4, length).ToArray());
            offset += 4 + length;
        }

        return strings;
    }
}
