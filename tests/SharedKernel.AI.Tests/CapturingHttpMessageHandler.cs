namespace SharedKernel.AI.Tests;

internal sealed class CapturingHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
{
    public HttpRequestMessage? Request { get; private set; }

    public string? RequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Request = request;
        RequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        return response;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            response.Dispose();
        }

        base.Dispose(disposing);
    }

}
