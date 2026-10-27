using Amazon.S3.Model;

namespace PandaBear.ObjectStorage.S3;

/// <summary>
/// Disposes the S3 response together with the stream returned to callers.
/// </summary>
/// <param name="response">The S3 object response that owns the stream.</param>
internal sealed class S3ObjectResponseStream(GetObjectResponse response) : Stream
{
    public override bool CanRead => response.ResponseStream.CanRead;
    public override bool CanSeek => response.ResponseStream.CanSeek;
    public override bool CanWrite => false;
    public override long Length => response.ResponseStream.Length;
    public override long Position
    {
        get => response.ResponseStream.Position; set => response.ResponseStream.Position = value;
    }
    public override void Flush() => response.ResponseStream.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => response.ResponseStream.FlushAsync(cancellationToken);
    public override int Read(byte[] buffer, int offset, int count) => response.ResponseStream.Read(buffer, offset, count);
    public override int Read(Span<byte> buffer) => response.ResponseStream.Read(buffer);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => response.ResponseStream.ReadAsync(buffer, cancellationToken);
    public override long Seek(long offset, SeekOrigin origin) => response.ResponseStream.Seek(offset, origin);
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            response.Dispose();
        }

        base.Dispose(disposing);
    }
}
