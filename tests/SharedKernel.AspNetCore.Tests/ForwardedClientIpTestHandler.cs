namespace PandaBear.AspNetCore.Tests;

/// <summary>HTTP message handler that delegates every request to a supplied function.</summary>
/// <param name="send">The function that produces the response for each request.</param>
internal sealed class ForwardedClientIpTestHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        send(request, cancellationToken);
}
