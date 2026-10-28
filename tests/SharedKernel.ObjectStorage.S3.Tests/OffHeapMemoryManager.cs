using System.Buffers;

namespace PandaBear.ObjectStorage.S3.Tests;

/// <summary>A <see cref="MemoryManager{T}"/> whose memory is not array-backed, so <c>MemoryMarshal.TryGetArray</c> fails.</summary>
/// <param name="bytes">The bytes to expose.</param>
internal sealed class OffHeapMemoryManager(byte[] bytes) : MemoryManager<byte>
{
    /// <inheritdoc />
    public override Span<byte> GetSpan() => bytes;

    /// <inheritdoc />
    public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Unpin()
    {
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
    }
}
