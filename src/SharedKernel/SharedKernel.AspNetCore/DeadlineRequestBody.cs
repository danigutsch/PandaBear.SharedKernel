// Adapted from ViajantesTurismo under its MIT license. See LICENSE.ViajantesTurismo.txt.

namespace SharedKernel.AspNetCore;

/// <summary>A non-owning request body view that applies the request deadline to reads.</summary>
internal sealed class DeadlineRequestBody(Stream body, CancellationToken ct) : Stream
{
    private readonly CancellationToken requestAborted = ct;

    public override bool CanRead => body.CanRead;
    public override bool CanSeek => body.CanSeek;
    public override bool CanWrite => false;
    public override long Length => body.Length;

    public override long Position
    {
        get => body.Position;
        set => body.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        requestAborted.ThrowIfCancellationRequested();
        return body.Read(buffer, offset, count);
    }

    public override int Read(Span<byte> buffer)
    {
        requestAborted.ThrowIfCancellationRequested();
        return body.Read(buffer);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        !cancellationToken.CanBeCanceled || cancellationToken == requestAborted
            ? body.ReadAsync(buffer, requestAborted)
            : ReadWithLinkedCancellation(buffer, cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => body.Seek(offset, origin);
    public override void Flush() => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private async ValueTask<int> ReadWithLinkedCancellation(Memory<byte> buffer, CancellationToken ct)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(requestAborted, ct);
        return await body.ReadAsync(buffer, cancellation.Token).ConfigureAwait(false);
    }
}
