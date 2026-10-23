using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.HttpOverrides;

namespace HttpRequests.Sample;

internal sealed class ForwardedAddressHandler : HttpMessageHandler
{
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
