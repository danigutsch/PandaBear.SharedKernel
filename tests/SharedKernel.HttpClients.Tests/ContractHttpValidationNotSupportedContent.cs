using System.Net;
using System.Net.Http.Headers;

namespace PandaBear.HttpClients.Tests;

/// <summary><c>text/plain</c> HTTP content that throws <see cref="NotSupportedException" /> when serialized or opened as a stream and records that it was touched.</summary>
internal sealed class ContractHttpValidationNotSupportedContent : HttpContent
{
    /// <summary>Creates the content with a <c>text/plain</c> content type.</summary>
    public ContractHttpValidationNotSupportedContent()
    {
        Headers.ContentType = new MediaTypeHeaderValue("text/plain");
    }

    /// <summary>Gets a value indicating whether serialization or stream creation was attempted.</summary>
    public bool WasRead
    {
        get; private set;
    }

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
