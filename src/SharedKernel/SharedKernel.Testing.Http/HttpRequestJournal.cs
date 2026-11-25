namespace SharedKernel.Testing.Http;

/// <summary>Thread-safe record of the requests a test handler received, with waits for a request count.</summary>
internal sealed class HttpRequestJournal
{
    private readonly Lock _gate = new();
    private readonly List<CapturedHttpRequest> _requests = [];
    private readonly List<(int Count, TaskCompletionSource<IReadOnlyList<CapturedHttpRequest>> Completion)> _waiters = [];

    /// <summary>Gets a snapshot of the recorded requests in arrival order.</summary>
    public IReadOnlyList<CapturedHttpRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>Gets the most recently recorded request, if any.</summary>
    public CapturedHttpRequest? LastRequest
    {
        get
        {
            lock (_gate)
            {
                return _requests.Count == 0 ? null : _requests[^1];
            }
        }
    }

    /// <summary>Buffers the request body, records the request and returns its capture.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <see langword="null"/>.</exception>
    public async Task<CapturedHttpRequest> Record(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // ReadAsStringAsync buffers the content, so later handlers can read it again.
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        return Add(request, body);
    }

    /// <summary>Buffers the request body synchronously, records the request and returns its capture.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <see langword="null"/>.</exception>
    public CapturedHttpRequest RecordFromSend(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // HttpContent has no synchronous buffering API; reading through the buffering path keeps the content re-readable.
        var body = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();

        return Add(request, body);
    }

    /// <summary>Completes once at least <paramref name="count"/> requests are recorded.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is less than 1.</exception>
    public Task<IReadOnlyList<CapturedHttpRequest>> WaitFor(int count, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        lock (_gate)
        {
            if (_requests.Count >= count)
            {
                return Task.FromResult<IReadOnlyList<CapturedHttpRequest>>([.. _requests.Take(count)]);
            }

            var completion = new TaskCompletionSource<IReadOnlyList<CapturedHttpRequest>>(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Add((count, completion));
            return completion.Task.WaitAsync(cancellationToken);
        }
    }

    private CapturedHttpRequest Add(HttpRequestMessage request, string? body)
    {
        var pathAndQuery = request.RequestUri switch
        {
            null => string.Empty,
            { IsAbsoluteUri: true } uri => uri.PathAndQuery,
            var uri => uri.OriginalString,
        };
        var captured = new CapturedHttpRequest(request, pathAndQuery, body);

        lock (_gate)
        {
            _requests.Add(captured);
            for (var index = _waiters.Count - 1; index >= 0; index--)
            {
                var (count, completion) = _waiters[index];
                if (_requests.Count >= count)
                {
                    completion.TrySetResult([.. _requests.Take(count)]);
                    _waiters.RemoveAt(index);
                }
            }
        }

        return captured;
    }
}
