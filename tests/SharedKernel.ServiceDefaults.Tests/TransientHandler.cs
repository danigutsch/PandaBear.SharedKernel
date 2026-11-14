using System.Net;
using System.Net.Http.Headers;

namespace PandaBear.ServiceDefaults.Tests;

/// <summary>Answers 503 with a zero Retry-After to the first attempts, then 200, and counts the attempts.</summary>
/// <param name="failures">The number of attempts that fail before a 200 is returned.</param>
internal sealed class TransientHandler(int failures = 1) : HttpMessageHandler
{
    /// <summary>Gets the number of requests the handler received.</summary>
    internal int Attempts { get; private set; }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Attempts++;
        var response = new HttpResponseMessage(Attempts <= failures ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
        return Task.FromResult(response);
    }
}
