using System.Net;
using System.Text;

namespace PandaBear.RepoConfig.Tests;

/// <summary>HTTP content that writes a fixed UTF-8 body to the request stream and then runs a callback, so a test can cancel after the body was sent.</summary>
/// <param name="content">The text written as the request body.</param>
/// <param name="afterWrite">Runs once the body has been written to the stream.</param>
internal sealed class CancellationAfterWriteHttpContent(string content, Action afterWrite) : HttpContent
{
    private readonly byte[] _content = Encoding.UTF8.GetBytes(content);

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(_content, cancellationToken).ConfigureAwait(false);
        afterWrite();
    }

    protected override bool TryComputeLength(out long length)
    {
        length = _content.Length;
        return true;
    }
}
