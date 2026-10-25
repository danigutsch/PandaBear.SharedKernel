namespace PandaBear.IntegrationTesting;

/// <summary>
/// Starts an expensive shared test resource when it is first requested, shares it among concurrent
/// borrowers, and disposes it once.
/// </summary>
/// <typeparam name="T">
/// The started resource, such as an <see cref="AspireTestApplication"/>, a connection string, or a marker value.
/// </typeparam>
/// <remarks>
/// <para>
/// The first <see cref="Get"/> invokes the start delegate on the calling thread and with a cancellation token
/// owned by this instance. The delegate runs synchronously up to its first <c>await</c> because Aspire locates its
/// DCP metadata by walking the synchronous call stack when the delegate creates a testing builder. Other callers
/// wait for that synchronous part, then share the same start. The delegate must use the supplied token rather than
/// ambient state of the first caller, such as a test's cancellation token, and must not call <see cref="Get"/> on
/// the same instance.
/// </para>
/// <para>
/// A failed start is cached: every caller observes the same failure and the delegate is not invoked again, so a
/// multi-minute startup timeout is not repeated for every borrower.
/// </para>
/// <para>
/// Dispose after every borrower has finished. Disposal never starts the resource, cancels and awaits a pending
/// start, and disposes the started value once. A start that failed before disposal belongs to the callers of
/// <see cref="Get"/>. A pending start that fails with anything but the cancellation requested by disposal, such as
/// a failure that carries cleanup errors, also surfaces from <see cref="DisposeAsync"/>, as do failures of the stop
/// delegate or of the value's own disposal.
/// </para>
/// </remarks>
public sealed class OnDemandResource<T> : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task<T>> _start;
    private readonly Func<T, ValueTask>? _stop;
    private readonly CancellationTokenSource _startCancellation = new();
    private readonly Lock _gate = new();
    private Task<T>? _started;
    private bool _disposed;

    /// <summary>
    /// Creates a resource that starts on its first <see cref="Get"/>.
    /// </summary>
    /// <param name="start">Starts the resource. It receives a token that disposal cancels while the start is pending.</param>
    /// <param name="stop">
    /// Stops the started value. When it is <see langword="null"/>, a value that implements
    /// <see cref="IAsyncDisposable"/> or <see cref="IDisposable"/> is disposed instead.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="start"/> is <see langword="null"/>.</exception>
    public OnDemandResource(Func<CancellationToken, Task<T>> start, Func<T, ValueTask>? stop = null)
    {
        ArgumentNullException.ThrowIfNull(start);
        _start = start;
        _stop = stop;
    }

    /// <summary>
    /// Gets a value indicating whether the start completed successfully and the resource is not disposed.
    /// </summary>
    public bool IsStarted
    {
        get
        {
            lock (_gate)
            {
                return !_disposed && _started is { IsCompletedSuccessfully: true };
            }
        }
    }

    /// <summary>
    /// Gets the started resource, starting it first when no caller has requested it yet.
    /// </summary>
    /// <param name="ct">
    /// Abandons only this caller's wait; it never cancels or poisons the shared start.
    /// </param>
    /// <returns>
    /// A task that completes with the shared resource, or with the cached failure of its start.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="ct"/> is already cancelled, before anything is started, and reported through the
    /// returned task when it is cancelled while the caller waits.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when the resource is disposed; nothing is started.</exception>
    public Task<T> Get(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        Task<T> started;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // ponytail: the gate is held while the delegate runs its synchronous part, so disposal cannot interleave
            // with the first start and concurrent callers wait for that part. Publish a TaskCompletionSource first and
            // run the delegate outside the gate if the synchronous part ever becomes slow enough to matter.
            started = _started ??= Begin(_start, _startCancellation.Token);
        }

        return started.WaitAsync(ct);
    }

    /// <summary>
    /// Releases the resource: a never-started resource is left alone, a pending start is cancelled and awaited
    /// (the cancellation it observes is swallowed, and any value it still produces is released), and a started
    /// value is stopped or disposed once. Later calls do nothing.
    /// </summary>
    /// <returns>
    /// A task that completes when the value is released. It faults with a failure of the stop delegate, of the
    /// value's own disposal, or of a pending start that did not fail by the cancellation this disposal requested.
    /// </returns>
    public async ValueTask DisposeAsync()
    {
        Task<T>? started;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            started = _started;
        }

        try
        {
            if (started is not null)
            {
                await Release(started).ConfigureAwait(false);
            }
        }
        finally
        {
            _startCancellation.Dispose();
        }
    }

    // The async modifier turns a synchronous throw of the delegate into a faulted task. The delegate still runs
    // synchronously on the calling thread up to its first await.
    private static async Task<T> Begin(Func<CancellationToken, Task<T>> start, CancellationToken token)
    {
        return await start(token).ConfigureAwait(false);
    }

    private async Task Release(Task<T> started)
    {
        T value;
        if (started.IsCompleted)
        {
            // The callers of Get already observed a start that failed or was cancelled; there is nothing to release.
            if (!started.IsCompletedSuccessfully)
            {
                return;
            }

            value = await started.ConfigureAwait(false);
        }
        else
        {
            await _startCancellation.CancelAsync().ConfigureAwait(false);
            try
            {
                value = await started.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_startCancellation.IsCancellationRequested)
            {
                // The pending start observed the cancellation that this disposal requested.
                return;
            }
        }

        await Stop(value).ConfigureAwait(false);
    }

    private async ValueTask Stop(T value)
    {
        if (_stop is not null)
        {
            await _stop(value).ConfigureAwait(false);
        }
        else if (value is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else if (value is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
