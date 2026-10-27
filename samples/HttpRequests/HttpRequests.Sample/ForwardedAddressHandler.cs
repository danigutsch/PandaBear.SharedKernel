using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.HttpOverrides;

namespace HttpRequests.Sample;

/// <summary>Test handler that answers every request with 503 and echoes the X-Forwarded-For header it received, so the sample can show address forwarding and retries.</summary>
internal sealed class ForwardedAddressHandler : HttpMessageHandler
{
    /// <summary>Number of requests the handler has received.</summary>
    public int AttemptCount
    {
        get; private set;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AttemptCount++;
        var forwarded = request.Headers.TryGetValues(ForwardedHeadersDefaults.XForwardedForHeaderName, out var addresses)
            ? string.Join(',', addresses)
            : "missing";
        var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent(forwarded)
        };
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
        return Task.FromResult(response);
    }
}
