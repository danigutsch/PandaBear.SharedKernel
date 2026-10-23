using System.Text;

namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>Minimal protobuf wire-format reader: varints, length-delimited fields, and skipping fixed-width fields.</summary>
/// <param name="buffer">Encoded message.</param>
internal struct ProtobufReader(ReadOnlyMemory<byte> buffer)
{
    private int position;

    /// <summary>Whether every byte was consumed.</summary>
    public readonly bool AtEnd => position >= buffer.Length;

    /// <summary>Reads a field tag.</summary>
    /// <param name="field">Field number.</param>
    /// <param name="wireType">Wire type.</param>
    /// <returns><see langword="true"/> when a valid tag was read.</returns>
    public bool TryReadTag(out int field, out int wireType)
    {
        field = 0;
        wireType = 0;
        if (!TryReadVarint(out ulong tag) || tag >> 3 is 0 or > int.MaxValue)
        {
            return false;
        }

        field = (int)(tag >> 3);
        wireType = (int)(tag & 7);
        return true;
    }

    /// <summary>Reads a base-128 varint of at most ten bytes.</summary>
    /// <param name="value">Value.</param>
    /// <returns><see langword="true"/> when complete.</returns>
    public bool TryReadVarint(out ulong value)
    {
        value = 0;
        ReadOnlySpan<byte> span = buffer.Span;
        for (int shift = 0; shift < 70; shift += 7)
        {
            if (position >= span.Length)
            {
                return false;
            }

            byte current = span[position++];
            value |= (ulong)(current & 0x7F) << shift;
            if ((current & 0x80) == 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reads a length-delimited field.</summary>
    /// <param name="value">Field bytes.</param>
    /// <returns><see langword="true"/> when the length fits the buffer.</returns>
    public bool TryReadLengthDelimited(out ReadOnlyMemory<byte> value)
    {
        value = default;
        if (!TryReadVarint(out ulong length) || length > (ulong)(buffer.Length - position))
        {
            return false;
        }

        value = buffer.Slice(position, (int)length);
        position += (int)length;
        return true;
    }

    /// <summary>Reads a bytes field.</summary>
    /// <param name="value">Copied bytes.</param>
    /// <returns><see langword="true"/> when complete.</returns>
    public bool TryReadBytes(out byte[] value)
    {
        bool ok = TryReadLengthDelimited(out ReadOnlyMemory<byte> slice);
        value = ok ? slice.ToArray() : [];
        return ok;
    }

    /// <summary>Reads a UTF-8 string field.</summary>
    /// <param name="value">Decoded string.</param>
    /// <returns><see langword="true"/> when complete.</returns>
    public bool TryReadString(out string value)
    {
        bool ok = TryReadLengthDelimited(out ReadOnlyMemory<byte> slice);
        value = ok ? Encoding.UTF8.GetString(slice.Span) : string.Empty;
        return ok;
    }

    /// <summary>Skips a field of the given wire type.</summary>
    /// <param name="wireType">Wire type.</param>
    /// <returns><see langword="true"/> when skipped; groups and unknown types are rejected.</returns>
    public bool TrySkip(int wireType)
    {
        return wireType switch
        {
            0 => TryReadVarint(out _),
            1 => Advance(8),
            2 => TryReadLengthDelimited(out _),
            5 => Advance(4),
            _ => false,
        };
    }

    private bool Advance(int count)
    {
        if (buffer.Length - position < count)
        {
            return false;
        }

        position += count;
        return true;
    }
}
