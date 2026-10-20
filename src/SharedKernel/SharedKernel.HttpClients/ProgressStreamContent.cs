using System.Net;

namespace SharedKernel.HttpClients;

/// <summary>
/// Serializes a known-length stream while reporting file bytes written to the HTTP transport.
/// </summary>
public sealed class ProgressStreamContent : HttpContent
{
    private const int CopyBufferSize = 81_920;
    private readonly Stream _content;
    private readonly long _contentLength;
    private readonly IProgress<long>? _progress;

    /// <summary>
    /// Initializes upload content for a known-length source stream.
    /// </summary>
    /// <param name="content">The source stream. Disposing this content also disposes the source.</param>
    /// <param name="contentLength">The authoritative declared file length.</param>
    /// <param name="progress">Receives cumulative file bytes after they are written to the transport.</param>
    public ProgressStreamContent(Stream content, long contentLength, IProgress<long>? progress)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfNegative(contentLength);

        _content = content;
        _contentLength = contentLength;
        _progress = progress;
    }

    /// <inheritdoc />
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return CopyToTransport(stream, CancellationToken.None);
    }

    /// <inheritdoc />
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return CopyToTransport(stream, cancellationToken);
    }

    /// <inheritdoc />
    protected override bool TryComputeLength(out long length)
    {
        length = _contentLength;
        return true;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _content.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task CopyToTransport(Stream destination, CancellationToken ct)
    {
        var buffer = new byte[CopyBufferSize];
        long transferred = 0;

        while (true)
        {
            var bytesRead = await _content.ReadAsync(buffer, ct).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                return;
            }

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
            transferred += bytesRead;
            _progress?.Report(transferred);
        }
    }
}
