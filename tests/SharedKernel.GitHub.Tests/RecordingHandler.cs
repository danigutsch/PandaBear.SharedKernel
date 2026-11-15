using System.Net;

namespace PandaBear.GitHub.Tests;

/// <summary>Answers every request with 200 OK and keeps the last request.</summary>
internal sealed class RecordingHandler : HttpMessageHandler
{
    /// <summary>Gets the last request sent.</summary>
    public HttpRequestMessage? Request { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Request = request;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
