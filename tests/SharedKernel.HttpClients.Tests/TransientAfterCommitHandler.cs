using System.Net;
using System.Net.Http.Headers;

namespace PandaBear.HttpClients.Tests;

internal sealed class TransientAfterCommitHandler(
    HttpStatusCode failureStatusCode = HttpStatusCode.ServiceUnavailable,
    int failuresBeforeSuccess = 1) : HttpMessageHandler
{
    public int AttemptCount
    {
        get; private set;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        AttemptCount++;
        if (request.Content is not null)
        {
            await request.Content.CopyToAsync(Stream.Null, cancellationToken).ConfigureAwait(false);
        }

        var response = new HttpResponseMessage(
            AttemptCount <= failuresBeforeSuccess ? failureStatusCode : HttpStatusCode.OK);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
        return response;
    }
}
