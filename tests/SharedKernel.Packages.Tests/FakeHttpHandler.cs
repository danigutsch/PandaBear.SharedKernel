using System.Net;
using System.Text;

namespace PandaBear.Packages.Tests;

/// <summary>An <see cref="HttpMessageHandler"/> that answers from a function and records every request; nothing reaches the network.</summary>
internal sealed class FakeHttpHandler(Func<RecordedRequest, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    private readonly List<RecordedRequest> _requests = [];
    private HttpClient? _client;

    /// <summary>Gets the requests in order.</summary>
    public IReadOnlyList<RecordedRequest> Requests => _requests;

    /// <summary>Creates a handler that answers synchronously.</summary>
    public static FakeHttpHandler Answering(Func<RecordedRequest, HttpResponseMessage> respond) =>
        new((request, _) => Task.FromResult(respond(request)));

    /// <summary>Creates a JSON response.</summary>
    public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    /// <summary>Gets a client over this handler; it is disposed with the handler.</summary>
    public HttpClient Client => _client ??= new HttpClient(this, disposeHandler: false);

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var recorded = new RecordedRequest(request.Method, request.RequestUri!, body, request.Headers.UserAgent.ToString());
        _requests.Add(recorded);
        var response = await respond(recorded, cancellationToken);
        response.RequestMessage ??= request;
        return response;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _client?.Dispose();
        }

        base.Dispose(disposing);
    }
}
