using System.Net;
using System.Net.Http.Headers;

namespace PandaBear.HttpClients.Tests;

/// <summary>
/// Test handler that reads the full request body and then fails with a retryable status for the first attempts before returning 200.
/// </summary>
/// <param name="failureStatusCode">The status returned for each failing attempt.</param>
/// <param name="failuresBeforeSuccess">The number of attempts that fail before a 200 is returned.</param>
internal sealed class TransientAfterCommitHandler(
    HttpStatusCode failureStatusCode = HttpStatusCode.ServiceUnavailable,
    int failuresBeforeSuccess = 1) : HttpMessageHandler
{
    /// <summary>Number of requests the handler has received.</summary>
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
