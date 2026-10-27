namespace PandaBear.HttpClients.Tests;

/// <summary>
/// Read-only stream over a byte array that records how many bytes and reads were requested and whether it was disposed.
/// </summary>
/// <param name="content">The bytes the stream serves.</param>
internal sealed class TrackingReadStream(byte[] content) : Stream
{
    private readonly MemoryStream _inner = new(content, writable: false);

    /// <summary>Total number of bytes returned by reads so far.</summary>
    public long BytesRead
    {
        get; private set;
    }

    /// <summary>Whether the stream has been disposed.</summary>
    public bool IsDisposed
    {
        get; private set;
    }

    /// <summary>Number of read calls made against the stream.</summary>
    public int ReadCount
    {
        get; private set;
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

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = _inner.Read(buffer, offset, count);
        RecordRead(bytesRead);
        return bytesRead;
    }

    public override int Read(Span<byte> buffer)
    {
        var bytesRead = _inner.Read(buffer);
        RecordRead(bytesRead);
        return bytesRead;
    }

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        var bytesRead = await _inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        RecordRead(bytesRead);
        return bytesRead;
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        var bytesRead = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        RecordRead(bytesRead);
        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            IsDisposed = true;
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }

    private void RecordRead(int bytesRead)
    {
        ReadCount++;
        BytesRead += bytesRead;
    }
}
