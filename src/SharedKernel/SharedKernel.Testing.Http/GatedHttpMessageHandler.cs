using System.Diagnostics.CodeAnalysis;

namespace SharedKernel.Testing.Http;

/// <summary>
/// Records each arriving request, with its body buffered, then holds it until <see cref="Release" /> opens the gate and
/// forwards it to the inner handler. Use it to observe code while a request is in flight. Safe for concurrent requests.
/// </summary>
/// <remarks>
/// A held request whose cancellation token fires throws <see cref="OperationCanceledException" /> and never reaches the
/// inner handler. Disposing the handler fails held requests with <see cref="ObjectDisposedException" /> and disposes the inner handler.
/// </remarks>
/// <param name="innerHandler">Answers released requests; this handler disposes it.</param>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="innerHandler"/> is <see langword="null"/>.</exception>
public sealed class GatedHttpMessageHandler(HttpMessageHandler innerHandler)
    : DelegatingHandler(innerHandler ?? throw new ArgumentNullException(nameof(innerHandler)))
{
    private readonly HttpRequestJournal _journal = new();
    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _pendingCount;

    /// <summary>Initializes a new instance of the <see cref="GatedHttpMessageHandler" /> class whose released requests get HTTP 200 with no content.</summary>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The delegating handler owns and disposes its inner handler.")]
    public GatedHttpMessageHandler()
        : this(new RecordingHttpMessageHandler())
    {
    }

    /// <summary>Gets a snapshot of the requests that arrived at the gate, held or released, in arrival order.</summary>
    public IReadOnlyList<CapturedHttpRequest> Requests => _journal.Requests;

    /// <summary>Gets the number of requests currently held at the gate.</summary>
    public int PendingCount => Volatile.Read(ref _pendingCount);

    /// <summary>Waits until at least <paramref name="count"/> requests have arrived at the gate.</summary>
    /// <param name="count">The number of requests to wait for.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The first <paramref name="count"/> arrived requests.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is less than 1.</exception>
    public Task<IReadOnlyList<CapturedHttpRequest>> WaitForRequests(int count, CancellationToken cancellationToken) =>
        _journal.WaitFor(count, cancellationToken);

    /// <summary>Opens the gate for held and later requests. Calling it again has no effect.</summary>
    public void Release() => _gate.TrySetResult();

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Count the request as held before recording it, so a completed wait observes it as pending.
        Interlocked.Increment(ref _pendingCount);
        try
        {
            await _journal.Record(request, cancellationToken).ConfigureAwait(false);
            await _gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref _pendingCount);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Count the request as held before recording it, so a completed wait observes it as pending.
        Interlocked.Increment(ref _pendingCount);
        try
        {
            _journal.RecordFromSend(request, cancellationToken);
            _gate.Task.WaitAsync(cancellationToken).GetAwaiter().GetResult();
        }
        finally
        {
            Interlocked.Decrement(ref _pendingCount);
        }

        return base.Send(request, cancellationToken);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _gate.TrySetException(new ObjectDisposedException(nameof(GatedHttpMessageHandler)));
        }

        base.Dispose(disposing);
    }
}
