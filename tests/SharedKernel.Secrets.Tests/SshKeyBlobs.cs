using System.Buffers.Binary;

namespace SharedKernel.Secrets.Tests;

/// <summary>Builds SSH public key blobs and recipient lines with chosen structure.</summary>
internal static class SshKeyBlobs
{
    /// <summary>An SSH string: a big-endian length and the bytes.</summary>
    public static byte[] Str(byte[] data)
    {
        var result = new byte[4 + data.Length];
        BinaryPrimitives.WriteUInt32BigEndian(result, (uint)data.Length);
        data.CopyTo(result, 4);
        return result;
    }

    /// <summary>A key blob: the type string and the fields.</summary>
    public static byte[] Blob(string type, params byte[][] fields) => [.. Str(System.Text.Encoding.ASCII.GetBytes(type)), .. fields.SelectMany(static f => f)];

    /// <summary>A recipient line for the type and blob.</summary>
    public static string Line(string type, byte[] blob) => type + " " + Convert.ToBase64String(blob);
}
