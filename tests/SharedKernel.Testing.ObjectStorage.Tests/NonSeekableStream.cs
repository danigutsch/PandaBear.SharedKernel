namespace SharedKernel.Testing.ObjectStorage.Tests;

/// <summary>A read-only stream that cannot seek, wrapping another stream.</summary>
/// <param name="inner">The stream that supplies the bytes; disposed with this stream.</param>
internal sealed class NonSeekableStream(Stream inner) : Stream
{
    /// <summary>Creates a non-seekable stream over the given bytes.</summary>
    /// <param name="content">The bytes the stream serves.</param>
    public NonSeekableStream(byte[] content)
        : this(new MemoryStream(content, writable: false))
    {
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => inner.ReadAsync(buffer, cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
