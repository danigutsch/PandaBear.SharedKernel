using System.Net;
using System.Net.Http.Headers;

namespace SharedKernel.HttpClients.Tests;

internal sealed class ContractHttpValidationNotSupportedContent : HttpContent
{
    public ContractHttpValidationNotSupportedContent()
    {
        Headers.ContentType = new MediaTypeHeaderValue("text/plain");
    }

    public bool WasRead { get; private set; }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        WasRead = true;
        throw new NotSupportedException("Test content cannot be serialized.");
    }

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return true;
    }

    protected override Task<Stream> CreateContentReadStreamAsync()
    {
        WasRead = true;
        throw new NotSupportedException("Test content cannot be read as a stream.");
    }
}
