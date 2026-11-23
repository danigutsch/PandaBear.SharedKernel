using Microsoft.Extensions.Time.Testing;

namespace PandaBear.Blazor.Tests;

/// <summary>A <see cref="FakeTimeProvider"/> that also counts the timers still waiting to fire, for code that creates its timer after an asynchronous step.</summary>
internal sealed class ManualTimeProvider() : FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))
{
    private readonly Lock _gate = new();
    private readonly List<PendingTimer> _timers = [];
    private readonly List<(DateTimeOffset Due, TaskCompletionSource Created)> _awaited = [];

    /// <summary>Gets the number of timers that have neither fired nor been disposed.</summary>
    public int PendingTimers
    {
        get
        {
            lock (_gate)
            {
                return _timers.Count(timer => timer.IsPending);
            }
        }
    }

    /// <summary>Waits until the given number of timers wait to fire.</summary>
    /// <param name="count">The number of pending timers.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>A task that completes when the timers exist.</returns>
    public async Task WaitForPendingTimers(int count, CancellationToken cancellationToken)
    {
        while (PendingTimers < count)
        {
            await Task.Delay(1, cancellationToken);
        }
    }

    /// <summary>
    /// Waits until a timer that fires at <paramref name="due"/> has been created, for code that creates its timer after an asynchronous
    /// step, such as a search that first awaits the cancellation of the search it replaces. The timer is told apart from the timers of
    /// older searches by when it fires, so the test moves the time only once this timer exists.
    /// </summary>
    /// <param name="due">When the timer fires: the time it was created plus its due time.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>A task that completes when the timer exists.</returns>
    public Task WaitForTimer(DateTimeOffset due, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_timers.Exists(timer => timer.Due == due))
            {
                return Task.CompletedTask;
            }

            var created = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _awaited.Add((due, created));
            return created.Task.WaitAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new PendingTimer(DueAt(dueTime));
        timer.Inner = base.CreateTimer(
            value =>
            {
                timer.MarkFired();
                callback(value);
            },
            state,
            dueTime,
            period);
        TaskCompletionSource[] created;
        lock (_gate)
        {
            _timers.Add(timer);
            created = [.. _awaited.Where(awaited => awaited.Due == timer.Due).Select(awaited => awaited.Created)];
            _awaited.RemoveAll(awaited => awaited.Due == timer.Due);
        }

        foreach (var waiter in created)
        {
            waiter.SetResult();
        }

        return timer;
    }

    private DateTimeOffset DueAt(TimeSpan dueTime) =>
        dueTime == Timeout.InfiniteTimeSpan ? DateTimeOffset.MaxValue : GetUtcNow() + dueTime;
}
