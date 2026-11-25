using System.Net;

namespace SharedKernel.Testing.Http;

/// <summary>
/// Records every request, with its body buffered, and answers it from routes keyed by method and exact path.
/// Unmatched requests get HTTP 404 or the fallback response. Safe for concurrent requests and for mapping while requests run.
/// </summary>
public sealed class RoutingHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpRequestJournal _journal = new();
    private readonly Lock _routesGate = new();
    private readonly Dictionary<(HttpMethod Method, string Path), Func<CapturedHttpRequest, HttpResponseMessage>> _routes = [];
    private readonly Func<CapturedHttpRequest, HttpResponseMessage> _fallback;

    /// <summary>Initializes a new instance of the <see cref="RoutingHttpMessageHandler" /> class that answers unmatched requests with HTTP 404.</summary>
    public RoutingHttpMessageHandler()
    {
        _fallback = static _ => new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    /// <summary>Initializes a new instance of the <see cref="RoutingHttpMessageHandler" /> class with a fallback for unmatched requests.</summary>
    /// <param name="fallback">Creates a new response for each request no route matches.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fallback"/> is <see langword="null"/>.</exception>
    public RoutingHttpMessageHandler(Func<CapturedHttpRequest, HttpResponseMessage> fallback)
    {
        ArgumentNullException.ThrowIfNull(fallback);

        _fallback = fallback;
    }

    /// <summary>Gets a snapshot of the recorded requests, matched or not, in arrival order.</summary>
    public IReadOnlyList<CapturedHttpRequest> Requests => _journal.Requests;

    /// <summary>Gets the most recently recorded request, if any.</summary>
    public CapturedHttpRequest? LastRequest => _journal.LastRequest;

    /// <summary>Waits until at least <paramref name="count"/> requests are recorded.</summary>
    /// <param name="count">The number of requests to wait for.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The first <paramref name="count"/> recorded requests.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is less than 1.</exception>
    public Task<IReadOnlyList<CapturedHttpRequest>> WaitForRequests(int count, CancellationToken cancellationToken) =>
        _journal.WaitFor(count, cancellationToken);

    /// <summary>Maps a method and path to a response delegate, replacing an earlier route for the same pair.</summary>
    /// <param name="method">The request method.</param>
    /// <param name="path">
    /// The absolute request path, such as <c>/api/items</c>, compared ordinally and case-sensitively with the escaped request path.
    /// The query is ignored when matching.
    /// </param>
    /// <param name="respond">Creates a new response for each matching request.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> does not start with <c>/</c> or contains a query or fragment.</exception>
    public void Map(HttpMethod method, string path, Func<CapturedHttpRequest, HttpResponseMessage> respond)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(respond);
        if (!path.StartsWith('/', StringComparison.Ordinal) || path.AsSpan().IndexOfAny('?', '#') >= 0)
        {
            throw new ArgumentException("The route path must be an absolute path without a query or fragment.", nameof(path));
        }

        lock (_routesGate)
        {
            _routes[(method, path)] = respond;
        }
    }

    /// <summary>Maps a method and path to a fixed status with no content, replacing an earlier route for the same pair.</summary>
    /// <param name="method">The request method.</param>
    /// <param name="path">The absolute request path; see <see cref="Map(HttpMethod, string, Func{CapturedHttpRequest, HttpResponseMessage})" />.</param>
    /// <param name="statusCode">The response status.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="method"/> or <paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> does not start with <c>/</c> or contains a query or fragment.</exception>
    public void Map(HttpMethod method, string path, HttpStatusCode statusCode) =>
        Map(method, path, _ => new HttpResponseMessage(statusCode));

    /// <summary>Maps a method and path to a UTF-8 <c>application/json</c> response, replacing an earlier route for the same pair.</summary>
    /// <param name="method">The request method.</param>
    /// <param name="path">The absolute request path; see <see cref="Map(HttpMethod, string, Func{CapturedHttpRequest, HttpResponseMessage})" />.</param>
    /// <param name="json">The JSON body.</param>
    /// <param name="statusCode">The response status.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="method"/>, <paramref name="path"/>, or <paramref name="json"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> does not start with <c>/</c> or contains a query or fragment.</exception>
    public void MapJson(HttpMethod method, string path, string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        ArgumentNullException.ThrowIfNull(json);

        Map(method, path, _ => HttpResponseFactory.Json(json, statusCode));
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var captured = await _journal.Record(request, cancellationToken).ConfigureAwait(false);

        return HttpResponseFactory.Create(Resolve(request), captured);
    }

    /// <inheritdoc />
    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var captured = _journal.RecordFromSend(request, cancellationToken);

        return HttpResponseFactory.Create(Resolve(request), captured);
    }

    private Func<CapturedHttpRequest, HttpResponseMessage> Resolve(HttpRequestMessage request)
    {
        if (request.RequestUri is not { IsAbsoluteUri: true } uri)
        {
            return _fallback;
        }

        lock (_routesGate)
        {
            return _routes.GetValueOrDefault((request.Method, uri.AbsolutePath), _fallback);
        }
    }
}
