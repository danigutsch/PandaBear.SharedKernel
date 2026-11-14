using System.Net;

namespace PandaBear.Testing.Http;

/// <summary>
/// Records every request, with its body buffered, and answers each one with a new response, so a test
/// does not need to know in advance how many requests the code under test sends. Safe for concurrent requests.
/// </summary>
public sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpRequestJournal _journal = new();
    private readonly Func<CapturedHttpRequest, HttpResponseMessage> _respond;

    /// <summary>Initializes a new instance of the <see cref="RecordingHttpMessageHandler" /> class that answers HTTP 200 with no content.</summary>
    public RecordingHttpMessageHandler()
        : this(HttpStatusCode.OK)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RecordingHttpMessageHandler" /> class that answers a fixed status with no content.</summary>
    /// <param name="statusCode">The status of every response.</param>
    public RecordingHttpMessageHandler(HttpStatusCode statusCode)
    {
        _respond = _ => new HttpResponseMessage(statusCode);
    }

    /// <summary>Initializes a new instance of the <see cref="RecordingHttpMessageHandler" /> class that answers with a delegate.</summary>
    /// <param name="respond">
    /// Creates a new response for each recorded request; its body is already buffered in <see cref="CapturedHttpRequest.Body" />.
    /// An exception it throws propagates to the caller, and the request stays recorded.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="respond"/> is <see langword="null"/>.</exception>
    public RecordingHttpMessageHandler(Func<CapturedHttpRequest, HttpResponseMessage> respond)
    {
        ArgumentNullException.ThrowIfNull(respond);

        _respond = respond;
    }

    /// <summary>Gets a snapshot of the recorded requests in arrival order.</summary>
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

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var captured = await _journal.Record(request, cancellationToken).ConfigureAwait(false);

        return HttpResponseFactory.Create(_respond, captured);
    }

    /// <inheritdoc />
    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var captured = _journal.RecordFromSend(request, cancellationToken);

        return HttpResponseFactory.Create(_respond, captured);
    }
}
