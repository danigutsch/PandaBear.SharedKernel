namespace SharedKernel.Aspire.Hosting.Tests;

/// <summary>Creates clients that send through one handler, which the clients do not dispose.</summary>
/// <param name="handler">The handler.</param>
internal sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    /// <inheritdoc />
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}
