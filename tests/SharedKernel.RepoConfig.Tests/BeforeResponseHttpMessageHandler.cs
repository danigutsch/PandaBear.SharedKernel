namespace PandaBear.RepoConfig.Tests;

/// <summary>An HTTP handler that runs a callback, honors cancellation, and then returns a freshly created response.</summary>
/// <param name="createResponse">Creates the response returned for every request.</param>
/// <param name="beforeResponse">Runs before the cancellation check and the response, so a test can cancel or change state mid-request.</param>
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
