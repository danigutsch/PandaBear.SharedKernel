using System.Net;
using System.Net.Http.Headers;

namespace SharedKernel.HttpClients.Tests;

internal sealed class TransientAfterCommitHandler : HttpMessageHandler
{
    public int AttemptCount { get; private set; }

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
            AttemptCount == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
        return response;
    }
}
