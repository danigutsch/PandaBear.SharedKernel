using System.Net;
using System.Text;

namespace SharedKernel.RepoConfig.Tests;

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
