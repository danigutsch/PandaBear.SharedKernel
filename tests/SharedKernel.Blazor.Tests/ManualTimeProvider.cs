using Microsoft.Extensions.Time.Testing;

namespace PandaBear.Blazor.Tests;

/// <summary>A <see cref="FakeTimeProvider"/> that also counts the timers still waiting to fire, for code that creates its timer after an asynchronous step.</summary>
internal sealed class ManualTimeProvider() : FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))
{
    private readonly Lock _gate = new();
    private readonly List<PendingTimer> _timers = [];

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

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new PendingTimer();
        timer.Inner = base.CreateTimer(
            value =>
            {
                timer.MarkFired();
                callback(value);
            },
            state,
            dueTime,
            period);
        lock (_gate)
        {
            _timers.Add(timer);
        }

        return timer;
    }
}
