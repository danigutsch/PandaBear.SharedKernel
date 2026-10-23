using System.Net;
using System.Security.Claims;
using HttpRequests.Sample;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using PandaBear.AspNetCore;
using PandaBear.HttpClients;
using PandaBear.Idempotency;

var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ApiAuthenticationDefaults.SubjectClaimType, "sample-subject")], "sample"));
var subject = principal.GetSubjectId() ?? throw new InvalidOperationException("Subject lookup failed.");
_ = IdempotencyKey.From(Guid.CreateVersion7().ToString("N"));

var submission = new IdempotentSubmission<string>();
string? originalKey = null;
var first = true;
await submission.Submit("sample request", (key, _) =>
{
    originalKey ??= key;
    if (key != originalKey)
    {
        throw new InvalidOperationException("Retry changed its idempotency key.");
    }
    var kind = first ? HttpOutcomeKind.TransportFailure : HttpOutcomeKind.Succeeded;
    first = false;
    return Task.FromResult(new HttpOutcome<string> { Kind = kind, Value = subject });
}, CancellationToken.None);
var retried = await submission.Retry(CancellationToken.None);
if (retried?.Kind != HttpOutcomeKind.Succeeded || submission.HasPending)
{
    throw new InvalidOperationException("Explicit retry failed.");
}

using var handler = new ForwardedAddressHandler();
var services = new ServiceCollection();
services.AddServiceDiscovery();
services.AddHttpClientDefaults(options => options.Retry.MaxRetryAttempts = 1);
services.AddHttpClient("forwarded-sample", client => client.BaseAddress = new Uri("https://sample.example.test"))
    .AddForwardedClientIp()
    .ConfigurePrimaryHttpMessageHandler(() => handler);
using var provider = services.BuildServiceProvider();
var contextAccessor = provider.GetRequiredService<IHttpContextAccessor>();
using var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient("forwarded-sample");
var visitors = new[]
{
    (Address: "::ffff:203.0.113.10", Forwarded: "203.0.113.10"),
    (Address: "203.0.113.20", Forwarded: "203.0.113.20")
};
for (var index = 0; index < visitors.Length; index++)
{
    var context = new DefaultHttpContext();
    context.Connection.RemoteIpAddress = IPAddress.Parse(visitors[index].Address);
    contextAccessor.HttpContext = context;
    using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/content", UriKind.Relative));
    request.Headers.Add(ForwardedHeadersDefaults.XForwardedForHeaderName, "198.51.100.99");

    using var response = await httpClient.SendAsync(request, CancellationToken.None);
    var forwarded = await response.Content.ReadAsStringAsync(CancellationToken.None);
    if (response.StatusCode != HttpStatusCode.ServiceUnavailable
        || forwarded != visitors[index].Forwarded
        || handler.AttemptCount != (index + 1) * 2)
    {
        throw new InvalidOperationException("Resolved address forwarding or customized retries failed.");
    }
}
contextAccessor.HttpContext = null;
