// Adapted from ViajantesTurismo under its MIT license. See LICENSE.ViajantesTurismo.txt.

using System.Net;

namespace PandaBear.Testing.Http;

/// <summary>
/// Request content that sends a fixed payload in chunks, holding each chunk back until the test releases it.
/// </summary>
public sealed class GatedChunkContent : HttpContent
{
    private readonly ReadOnlyMemory<byte> payload;
    private readonly int chunkSize;
    private readonly bool declareLength;
    private readonly SemaphoreSlim gate = new(0);
    private int chunksStarted;

    /// <summary>Creates content for the payload, split into chunks of at most <paramref name="chunkSize"/> bytes.</summary>
    /// <param name="payload">The bytes to send. The caller keeps them unchanged until sending completes.</param>
    /// <param name="chunkSize">The maximum number of payload bytes per write.</param>
    /// <param name="contentType">The content type header, including the boundary for multipart bodies.</param>
    /// <param name="declareLength">Whether to report a known body length. Use false to exercise HTTP/1.1 chunked framing.</param>
    /// <exception cref="ArgumentOutOfRangeException">The chunk size is less than one.</exception>
    /// <exception cref="ArgumentException">The content type is null or blank.</exception>
    public GatedChunkContent(ReadOnlyMemory<byte> payload, int chunkSize, string contentType, bool declareLength = true)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(chunkSize, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        this.payload = payload;
        this.chunkSize = chunkSize;
        this.declareLength = declareLength;
        Headers.TryAddWithoutValidation("Content-Type", contentType);
    }

    /// <summary>Gets the number of chunks the sender has begun to write.</summary>
    public int ChunksStarted => Volatile.Read(ref chunksStarted);

    /// <summary>Gets the number of chunks the payload is split into.</summary>
    public int ChunkCount => (int)((payload.Length + (long)chunkSize - 1) / chunkSize);

    /// <summary>Gets the number of payload bytes sent once <paramref name="chunks"/> chunks are written.</summary>
    /// <param name="chunks">The number of released chunks.</param>
    /// <returns>The number of bytes, capped at the payload length.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The chunk count is negative.</exception>
    public long BytesAfter(int chunks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(chunks);
        return Math.Min(payload.Length, (long)chunks * chunkSize);
    }

    /// <summary>Lets the sender write exactly one more chunk.</summary>
    public void ReleaseChunk() => gate.Release();

    /// <inheritdoc />
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        SerializeToStreamAsync(stream, context, CancellationToken.None);

    /// <inheritdoc />
    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var offset = 0;
        while (offset < payload.Length)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            _ = Interlocked.Increment(ref chunksStarted);
            var count = Math.Min(chunkSize, payload.Length - offset);
            await stream.WriteAsync(payload.Slice(offset, count), cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            offset += count;
        }
    }

    /// <inheritdoc />
    protected override bool TryComputeLength(out long length)
    {
        length = declareLength ? payload.Length : 0;
        return declareLength;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            gate.Dispose();
        }

        base.Dispose(disposing);
    }
}
