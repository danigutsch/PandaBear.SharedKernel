using System.Net;

namespace PandaBear.Testing.Http;

/// <summary>
/// HTTP content of unknown length that copies a source stream and counts how often it is serialized.
/// </summary>
/// <param name="source">The stream copied to the request on each serialization. A repeated serialization copies from the source's current position, so rewind a seekable source between sends; a non-seekable source yields no bytes the second time.</param>
public sealed class TrackingStreamContent(Stream source) : HttpContent
{
    private int _serializationCount;

    /// <summary>Number of times the content has been serialized to a request stream.</summary>
    public int SerializationCount => Volatile.Read(ref _serializationCount);

    /// <inheritdoc />
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        return SerializeToStreamAsync(stream, context, CancellationToken.None);
    }

    /// <inheritdoc />
    protected override async Task SerializeToStreamAsync(
        Stream stream,
        TransportContext? context,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _serializationCount);
        await source.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            source.Dispose();
        }

        base.Dispose(disposing);
    }
}
