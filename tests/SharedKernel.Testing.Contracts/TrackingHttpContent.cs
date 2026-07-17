using System.Net;

namespace SharedKernel.Testing.Contracts;

/// <summary>
/// HTTP content that records whether it has been disposed.
/// </summary>
public sealed class TrackingHttpContent : HttpContent
{
    /// <summary>
    /// Gets a value indicating whether disposal has occurred.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return true;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            IsDisposed = true;
        }

        base.Dispose(disposing);
    }
}
