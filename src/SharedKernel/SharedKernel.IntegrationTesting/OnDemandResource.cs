using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

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
/// ambient state of the first caller, such as a test's cancellation token. It must not call <see cref="Get"/> or
/// <see cref="DisposeAsync"/> on the same instance: a <see cref="Get"/> from its synchronous part throws
/// <see cref="InvalidOperationException"/>, and any later call would wait for the start itself and never complete.
/// </para>
/// <para>
/// A failed start is cached: every caller observes the same failure and the delegate is not invoked again, so a
/// multi-minute startup timeout is not repeated for every borrower. A start that is cancelled by anything but
/// disposal, such as its own startup timeout, fails with an <see cref="InvalidOperationException"/> that carries
/// the cancellation as its inner exception, so the cached failure says what happened.
/// </para>
/// <para>
/// Dispose after every borrower has finished. Disposal never starts the resource. It cancels and awaits a pending
/// start, and every cancellation it observes from that start counts as the one it requested. It disposes the
/// started value once, through <see cref="IAsyncDisposable"/> when the value implements it and through
/// <see cref="IDisposable"/> otherwise. A start that failed before disposal belongs to the callers of
/// <see cref="Get"/>; when none of them received that failure, <see cref="DisposeAsync"/> reports it. A pending start
/// that fails with anything but a cancellation, such as a failure that carries cleanup errors, also surfaces from
/// <see cref="DisposeAsync"/>, as do failures of cancellation callbacks and of the value's own disposal; several
/// failures surface together in an <see cref="AggregateException"/>. Overlapping and later calls wait for the first
/// disposal and never repeat its failure.
/// </para>
/// </remarks>
public sealed class OnDemandResource<T> : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task<T>> _start;
    private readonly CancellationTokenSource _startCancellation = new();
    private readonly Lock _gate = new();
    private Task<T>? _started;
    private Task? _disposal;
    private bool _startFailureReported;

    /// <summary>
    /// Creates a resource that starts on its first <see cref="Get"/>.
    /// </summary>
    /// <param name="start">Starts the resource. It receives a token that disposal cancels while the start is pending.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="start"/> is <see langword="null"/>.</exception>
    public OnDemandResource(Func<CancellationToken, Task<T>> start)
    {
        ArgumentNullException.ThrowIfNull(start);
        _start = start;
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
    /// <exception cref="InvalidOperationException">
    /// Thrown when the start delegate calls this method on the same instance from its synchronous part.
    /// </exception>
    public Task<T> Get(CancellationToken ct)
    {
        // Lock is recursive and nothing is published until the delegate returns, so a nested call would start again, forever.
        if (_gate.IsHeldByCurrentThread)
        {
            throw new InvalidOperationException("The start delegate of an on-demand resource must not call Get on the same instance.");
        }

        ct.ThrowIfCancellationRequested();

        Task<T> started;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposal is not null, this);

            // ponytail: the gate is held while the delegate runs its synchronous part, so disposal cannot interleave
            // with the first start and concurrent callers wait for that part. Publish a TaskCompletionSource first and
            // run the delegate outside the gate if the synchronous part ever becomes slow enough to matter.
            started = _started ??= Begin(_start, _startCancellation.Token);
        }

        return Wait(started, ct);
    }

    /// <summary>
    /// Releases the resource: a never-started resource is left alone, a pending start is cancelled and awaited
    /// (any cancellation it then observes is swallowed, and any value it still produces is released), and a started
    /// value is disposed once. Overlapping and later calls wait for that first disposal.
    /// </summary>
    /// <returns>
    /// A task that completes when the value is released. For the first call it faults with a failure of a
    /// cancellation callback, of the value's own disposal, of a pending start that did not fail by cancellation, or
    /// of a start that failed before disposal and was received by no caller of <see cref="Get"/>; several failures
    /// are combined in an <see cref="AggregateException"/>. Overlapping and later calls complete without repeating a
    /// failure.
    /// </returns>
    public ValueTask DisposeAsync()
    {
        TaskCompletionSource released;
        Task<T>? started;
        lock (_gate)
        {
            if (_disposal is { } disposal)
            {
                return new ValueTask(disposal);
            }

            released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _disposal = released.Task;
            started = _started;
        }

        return new ValueTask(Teardown(started, released));
    }

    // The async modifier turns a synchronous throw of the delegate into a faulted task. The delegate still runs
    // synchronously on the calling thread up to its first await.
    private static async Task<T> Begin(Func<CancellationToken, Task<T>> start, CancellationToken token)
    {
        try
        {
            return await start(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException cancellation) when (!token.IsCancellationRequested)
        {
            // Only disposal cancels this token. Any other cancellation, such as the start's own startup timeout,
            // would reach every caller as a bare "A task was canceled." that names no cause.
            throw new InvalidOperationException(
                "The on-demand resource start was cancelled although disposal did not request it, for example by its own startup timeout.",
                cancellation);
        }
    }

    private async Task<T> Wait(Task<T> started, CancellationToken ct)
    {
        try
        {
            return await started.WaitAsync(ct).ConfigureAwait(false);
        }
        catch when (started.IsCompleted && !started.IsCompletedSuccessfully)
        {
            // The caller is about to receive the failure of the start, so disposal does not report it again.
            Volatile.Write(ref _startFailureReported, true);
            throw;
        }
    }

    private async Task Teardown(Task<T>? started, TaskCompletionSource released)
    {
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

            // Overlapping calls wait for this outcome but never repeat its failure, so this task never faults.
            released.SetResult();
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Disposal must release a started value before it reports any failure.")]
    private async Task Release(Task<T> started)
    {
        List<Exception> failures = [];
        if (!started.IsCompleted)
        {
            try
            {
                await _startCancellation.CancelAsync().ConfigureAwait(false);
            }
            catch (Exception failure)
            {
                // A callback registered on the start token threw. The token is cancelled all the same, so the start is
                // still awaited and its value released before this failure is reported.
                failures.Add(failure);
            }
        }
        else if (started.IsFaulted && Volatile.Read(ref _startFailureReported))
        {
            // A caller of Get already received this failure.
            return;
        }

        var value = default(T)!;
        var produced = false;
        try
        {
            value = await started.ConfigureAwait(false);
            produced = true;
        }
        catch (OperationCanceledException)
        {
            // Begin turns every cancellation that disposal did not request into an InvalidOperationException, so this
            // one is the cancellation requested above, whichever token the start observed it through.
        }
        catch (Exception failure)
        {
            failures.Add(failure);
        }

        if (produced)
        {
            try
            {
                await DisposeValue(value).ConfigureAwait(false);
            }
            catch (Exception failure)
            {
                failures.Add(failure);
            }
        }

        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Throw(failures[0]);
        }

        if (failures.Count > 1)
        {
            throw new AggregateException("On-demand resource disposal failed.", failures);
        }
    }

    private static async ValueTask DisposeValue(T value)
    {
        if (value is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else if (value is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
