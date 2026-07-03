using System.Net;

namespace SharedKernel.HttpClients.Tests;

internal sealed class ContractHttpValidationNotSupportedContent : HttpContent
{
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        throw new NotSupportedException("Test content cannot be serialized.");
    }

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return true;
    }

    protected override Task<Stream> CreateContentReadStreamAsync()
    {
        throw new NotSupportedException("Test content cannot be read as a stream.");
    }
}
