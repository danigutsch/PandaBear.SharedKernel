using System.Buffers.Binary;

namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>Reads SSH wire-format <c>uint32</c> and <c>string</c> values (RFC 4251).</summary>
/// <param name="buffer">Encoded data.</param>
internal struct SshWireReader(ReadOnlyMemory<byte> buffer)
{
    private int position;

    /// <summary>Whether every byte was consumed.</summary>
    public readonly bool AtEnd => position >= buffer.Length;

    /// <summary>Reads a big-endian <c>uint32</c>.</summary>
    /// <param name="value">Value.</param>
    /// <returns><see langword="true"/> when four bytes remain.</returns>
    public bool TryReadUInt32(out uint value)
    {
        value = 0;
        if (buffer.Length - position < 4)
        {
            return false;
        }

        value = BinaryPrimitives.ReadUInt32BigEndian(buffer.Span[position..]);
        position += 4;
        return true;
    }

    /// <summary>Reads a length-prefixed string.</summary>
    /// <param name="value">Copied bytes.</param>
    /// <returns><see langword="true"/> when the length fits.</returns>
    public bool TryReadString(out byte[] value)
    {
        value = [];
        if (!TryReadUInt32(out uint length) || length > (uint)(buffer.Length - position))
        {
            return false;
        }

        value = buffer.Slice(position, (int)length).ToArray();
        position += (int)length;
        return true;
    }
}
