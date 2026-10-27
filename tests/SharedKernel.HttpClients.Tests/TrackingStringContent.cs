using System.Text;

namespace PandaBear.HttpClients.Tests;

/// <summary>
/// String content that records when it is disposed.
/// </summary>
/// <param name="content">The text payload.</param>
/// <param name="mediaType">The media type of the content.</param>
internal sealed class TrackingStringContent(string content, string mediaType) : StringContent(content, Encoding.UTF8, mediaType)
{
    /// <summary>Whether the content has been disposed.</summary>
    public bool IsDisposed
    {
        get; private set;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            IsDisposed = true;
        }

        base.Dispose(disposing);
    }
}
