using System.Net;

namespace PandaBear.HttpClients.Tests;

/// <summary>
/// HTTP content of unknown length that copies a source stream and counts how often it is serialized.
/// </summary>
/// <param name="source">The stream copied to the request on each serialization.</param>
internal sealed class TrackingStreamContent(Stream source) : HttpContent
{
    /// <summary>Number of times the content has been serialized to a request stream.</summary>
    public int SerializationCount
    {
        get; private set;
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        return SerializeToStreamAsync(stream, context, CancellationToken.None);
    }

    protected override async Task SerializeToStreamAsync(
        Stream stream,
        TransportContext? context,
        CancellationToken cancellationToken)
    {
        SerializationCount++;
        await source.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            source.Dispose();
        }

        base.Dispose(disposing);
    }
}
