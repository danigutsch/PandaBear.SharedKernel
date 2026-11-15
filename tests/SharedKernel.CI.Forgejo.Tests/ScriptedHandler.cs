using System.Net;

namespace PandaBear.CI.Forgejo.Tests;

/// <summary>Answers each request with the next scripted response, or 404 when the script is exhausted, and records the requests.</summary>
internal sealed class ScriptedHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _responses = new();

    /// <summary>Gets the requests in the order they were sent.</summary>
    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>Queues a response with <paramref name="status"/>, a JSON body, and a <c>Link</c> header.</summary>
    public ScriptedHandler Respond(HttpStatusCode status, string? json = null, string? link = null) =>
        Respond((_, _) =>
        {
            var response = new HttpResponseMessage(status)
            {
                Content = json is null ? null : new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            };
            if (link is not null)
            {
                response.Headers.TryAddWithoutValidation("Link", link.Trim());
            }

            return Task.FromResult(response);
        });

    /// <summary>Queues a response that <paramref name="respond"/> produces.</summary>
    public ScriptedHandler Respond(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        _responses.Enqueue(respond);
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return _responses.TryDequeue(out var respond) ? respond(request, cancellationToken) : Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
