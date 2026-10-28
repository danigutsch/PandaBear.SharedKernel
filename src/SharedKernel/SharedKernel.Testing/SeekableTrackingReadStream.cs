namespace PandaBear.Testing;

/// <summary>Test stream over a byte array that counts bytes read, records disposal and can pretend to be non-seekable.</summary>
/// <param name="content">The bytes the stream serves.</param>
/// <param name="seekable">Whether the stream reports and supports seeking, length and position.</param>
public sealed class SeekableTrackingReadStream(byte[] content, bool seekable) : Stream
{
    private readonly MemoryStream inner = new(content);
    /// <summary>Gets the total number of bytes returned by all reads so far.</summary>
    public long BytesRead
    {
        get; private set;
    }
    /// <summary>Gets a value indicating whether the stream has been disposed.</summary>
    public bool IsDisposed
    {
        get; private set;
    }
    /// <inheritdoc />
    public override bool CanRead => !IsDisposed;
    /// <inheritdoc />
    public override bool CanSeek => seekable;
    /// <inheritdoc />
    public override bool CanWrite => false;
    /// <inheritdoc />
    public override long Length => seekable ? inner.Length : throw new NotSupportedException();
    /// <inheritdoc />
    public override long Position
    {
        get => seekable ? inner.Position : throw new NotSupportedException();
        set
        {
            if (!seekable)
            {
                throw new NotSupportedException();
            }
            inner.Position = value;
        }
    }
    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = inner.Read(buffer, offset, count);
        BytesRead += read;
        return read;
    }
    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var read = await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        BytesRead += read;
        return read;
    }
    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => seekable ? inner.Seek(offset, origin) : throw new NotSupportedException();
    /// <inheritdoc />
    public override void Flush()
    {
    }
    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();
    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            IsDisposed = true;
            inner.Dispose();
        }
        base.Dispose(disposing);
    }
}
