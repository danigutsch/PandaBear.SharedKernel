namespace SharedKernel.HttpClients.Tests;

internal sealed class ContractCommandOutcomeTestHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    public CancellationToken ObservedCancellationToken { get; private set; }

    public HttpMethod? ObservedMethod { get; private set; }

    public Uri? ObservedRequestUri { get; private set; }

    public string? ObservedAuthorizationScheme { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ObservedCancellationToken = cancellationToken;
        ObservedMethod = request.Method;
        ObservedRequestUri = request.RequestUri;
        ObservedAuthorizationScheme = request.Headers.Authorization?.Scheme;
        return send(request, cancellationToken);
    }
}
