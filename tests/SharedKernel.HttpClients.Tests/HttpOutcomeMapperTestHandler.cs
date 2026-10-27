namespace PandaBear.HttpClients.Tests;

/// <summary>HTTP message handler that records the method, URI, authorization scheme and cancellation token of the last request and delegates the response to a supplied function.</summary>
/// <param name="send">The function that produces the response for each request.</param>
internal sealed class HttpOutcomeMapperTestHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    /// <summary>Gets the cancellation token passed with the last request.</summary>
    public CancellationToken ObservedCancellationToken
    {
        get; private set;
    }

    /// <summary>Gets the HTTP method of the last request.</summary>
    public HttpMethod? ObservedMethod
    {
        get; private set;
    }

    /// <summary>Gets the request URI of the last request.</summary>
    public Uri? ObservedRequestUri
    {
        get; private set;
    }

    /// <summary>Gets the scheme of the <c>Authorization</c> header of the last request, or <see langword="null" /> when it had none.</summary>
    public string? ObservedAuthorizationScheme
    {
        get; private set;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ObservedCancellationToken = cancellationToken;
        ObservedMethod = request.Method;
        ObservedRequestUri = request.RequestUri;
        ObservedAuthorizationScheme = request.Headers.Authorization?.Scheme;
        return send(request, cancellationToken);
    }
}
