using System.Text;

namespace PandaBear.HttpClients.Tests;

internal sealed class TrackingStringContent(string content, string mediaType) : StringContent(content, Encoding.UTF8, mediaType)
{
    public bool IsDisposed { get; private set; }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            IsDisposed = true;
        }

        base.Dispose(disposing);
    }
}
