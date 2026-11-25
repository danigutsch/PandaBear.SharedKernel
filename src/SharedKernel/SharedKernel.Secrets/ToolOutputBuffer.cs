using System.Security.Cryptography;

namespace SharedKernel.Secrets;

/// <summary>A growing byte buffer with a size limit (over it: overflow) that overwrites every array it stops using.</summary>
internal sealed class ToolOutputBuffer
{
    private readonly int _limit;
    private byte[] _buffer = new byte[4096];
    private int _length;

    /// <summary>Creates a buffer.</summary>
    /// <param name="limit">The most bytes it keeps.</param>
    internal ToolOutputBuffer(int limit) => _limit = limit;

    /// <summary>Gets a value indicating whether more than the limit was offered.</summary>
    internal bool Overflowed
    {
        get; private set;
    }

    /// <summary>Adds bytes, or marks the buffer overflowed and clears it.</summary>
    internal void Append(ReadOnlySpan<byte> bytes)
    {
        if (Overflowed)
        {
            return;
        }

        if (bytes.Length > _limit - _length)
        {
            Overflowed = true;
            CryptographicOperations.ZeroMemory(_buffer);
            _length = 0;
            return;
        }

        if (_length + bytes.Length > _buffer.Length)
        {
            var bigger = new byte[Math.Min(_limit, Math.Max(_buffer.Length * 2, _length + bytes.Length))];
            _buffer.AsSpan(0, _length).CopyTo(bigger);
            CryptographicOperations.ZeroMemory(_buffer);
            _buffer = bigger;
        }

        bytes.CopyTo(_buffer.AsSpan(_length));
        _length += bytes.Length;
    }

    /// <summary>Moves the content into a pinned array and clears the working buffer.</summary>
    internal SecretBytes Take()
    {
        var secret = SecretBytes.CopyOf(_buffer.AsSpan(0, _length));
        CryptographicOperations.ZeroMemory(_buffer);
        _length = 0;
        return secret;
    }

    /// <summary>Overwrites and forgets the content.</summary>
    internal void Clear()
    {
        CryptographicOperations.ZeroMemory(_buffer);
        _length = 0;
    }
}
