using System.Net;

namespace SharedKernel.Testing.Http;

/// <summary>
/// Captures HTTP requests and returns deterministic responses for tests.
/// </summary>
public sealed class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = [];
    private readonly List<CapturedHttpRequest> _requests = [];
    private readonly Exception? _sendException;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestHttpMessageHandler" /> class.
    /// </summary>
    public TestHttpMessageHandler()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestHttpMessageHandler" /> class with one response.
    /// </summary>
    /// <param name="response">The response to return.</param>
    public TestHttpMessageHandler(HttpResponseMessage response)
    {
        Enqueue(response);
    }

    private TestHttpMessageHandler(Exception sendException)
    {
        _sendException = sendException;
    }

    /// <summary>
    /// Gets the captured requests.
    /// </summary>
    public IReadOnlyList<CapturedHttpRequest> Requests => _requests;

    /// <summary>
    /// Gets the last captured request, if any.
    /// </summary>
    public CapturedHttpRequest? LastRequest => _requests.Count == 0 ? null : _requests[^1];

    /// <summary>
    /// Creates a handler that throws the provided exception when it sends a request.
    /// </summary>
    /// <param name="sendException">The exception to throw.</param>
    /// <returns>The throwing handler.</returns>
    public static TestHttpMessageHandler FromException(Exception sendException)
    {
        ArgumentNullException.ThrowIfNull(sendException);

        return new TestHttpMessageHandler(sendException);
    }

    /// <summary>
    /// Enqueues an HTTP response.
    /// </summary>
    /// <param name="response">The response to return.</param>
    public void Enqueue(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        _responses.Enqueue(response);
    }

    /// <summary>
    /// Enqueues a JSON HTTP response.
    /// </summary>
    /// <param name="statusCode">The status code.</param>
    /// <param name="json">The JSON body.</param>
    public void EnqueueJson(HttpStatusCode statusCode, string json) => Enqueue(HttpResponseFactory.Json(json, statusCode));

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_sendException is not null)
        {
            throw _sendException;
        }

        await Capture(request, cancellationToken).ConfigureAwait(false);

        return DequeueResponse();
    }

    /// <inheritdoc />
    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_sendException is not null)
        {
            throw _sendException;
        }

        CaptureFromSend(request, cancellationToken);

        return DequeueResponse();
    }

    private async Task Capture(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        AddRequest(request, body);
    }

    private void CaptureFromSend(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = ReadBodyFromContent(request.Content, cancellationToken);

        AddRequest(request, body);
    }

    private static string? ReadBodyFromContent(HttpContent? content, CancellationToken cancellationToken)
    {
        if (content is null)
        {
            return null;
        }

        using var stream = content.ReadAsStream(cancellationToken);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private void AddRequest(HttpRequestMessage request, string? body)
    {
        var requestUri = request.RequestUri;

        _requests.Add(new CapturedHttpRequest(
            request,
            requestUri?.PathAndQuery ?? string.Empty,
            body));
    }

    private HttpResponseMessage DequeueResponse() => _responses.Count == 0
        ? throw new InvalidOperationException("No HTTP response is queued for the captured request.")
        : _responses.Dequeue();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            while (_responses.Count > 0)
            {
                _responses.Dequeue().Dispose();
            }
        }

        base.Dispose(disposing);
    }
}
