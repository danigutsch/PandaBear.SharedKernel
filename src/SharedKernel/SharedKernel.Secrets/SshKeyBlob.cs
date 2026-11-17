using System.Buffers.Binary;
using System.Text;

namespace PandaBear.Secrets;

/// <summary>Checks that bytes are exactly one SSH public key of a given type, with nothing after it.</summary>
internal static class SshKeyBlob
{
    private const int Ed25519KeyLength = 32;

    /// <summary>Returns whether <paramref name="blob"/> is a well-formed <paramref name="type"/> key: ed25519 is a 32-byte string, rsa is two minimal positive integers, and nothing follows.</summary>
    internal static bool IsExactly(string type, ReadOnlySpan<byte> blob)
    {
        var rest = blob;
        if (!TryReadString(ref rest, out var name) || !name.SequenceEqual(Encoding.ASCII.GetBytes(type)))
        {
            return false;
        }

        if (type == "ssh-ed25519")
        {
            return TryReadString(ref rest, out var key) && key.Length == Ed25519KeyLength && rest.IsEmpty;
        }

        return TryReadString(ref rest, out var exponent) && IsPositiveMinimalInteger(exponent)
            && TryReadString(ref rest, out var modulus) && IsPositiveMinimalInteger(modulus)
            && rest.IsEmpty;
    }

    private static bool TryReadString(ref ReadOnlySpan<byte> data, out ReadOnlySpan<byte> value)
    {
        value = default;
        if (data.Length < 4)
        {
            return false;
        }

        var length = BinaryPrimitives.ReadUInt32BigEndian(data);
        if (length > (uint)(data.Length - 4))
        {
            return false;
        }

        value = data.Slice(4, (int)length);
        data = data[(4 + (int)length)..];
        return true;
    }

    private static bool IsPositiveMinimalInteger(ReadOnlySpan<byte> value) =>
        value.Length > 0
        && (value[0] & 0x80) == 0
        && (value[0] != 0 || (value.Length > 1 && (value[1] & 0x80) != 0));
}
