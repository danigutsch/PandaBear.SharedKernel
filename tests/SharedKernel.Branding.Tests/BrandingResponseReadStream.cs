namespace PandaBear.Branding.Tests;

internal sealed class BrandingResponseReadStream(byte[] content, bool seekable) : Stream
{
    private readonly MemoryStream inner = new(content);
    public long BytesRead
    {
        get; private set;
    }
    public bool IsDisposed
    {
        get; private set;
    }
    public override bool CanRead => !IsDisposed;
    public override bool CanSeek => seekable;
    public override bool CanWrite => false;
    public override long Length => seekable ? inner.Length : throw new NotSupportedException();
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
    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = inner.Read(buffer, offset, count);
        BytesRead += read;
        return read;
    }
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await inner.ReadAsync(buffer, cancellationToken);
        BytesRead += read;
        return read;
    }
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    public override long Seek(long offset, SeekOrigin origin) => seekable ? inner.Seek(offset, origin) : throw new NotSupportedException();
    public override void Flush()
    {
    }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
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
