using System.Buffers;

namespace PandaBear.IO.Tests;

/// <summary>A memory owner whose memory is not array-backed.</summary>
/// <param name="content">The bytes to expose.</param>
internal sealed class ManagerBackedMemory(byte[] content) : MemoryManager<byte>
{
    public override Span<byte> GetSpan() => content;

    public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();

    public override void Unpin()
    {
    }

    protected override void Dispose(bool disposing)
    {
    }
}
