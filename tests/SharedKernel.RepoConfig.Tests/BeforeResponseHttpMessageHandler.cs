namespace SharedKernel.RepoConfig.Tests;

internal sealed class BeforeResponseHttpMessageHandler(Func<HttpResponseMessage> createResponse, Action beforeResponse) : HttpMessageHandler
{
    private readonly Func<HttpResponseMessage> _createResponse = createResponse;
    private readonly Action _beforeResponse = beforeResponse;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _beforeResponse();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_createResponse());
    }
}
