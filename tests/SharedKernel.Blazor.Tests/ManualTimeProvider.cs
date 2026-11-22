namespace PandaBear.Blazor.Tests;

/// <summary>A time provider whose timers fire only when the test advances the time.</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Gets the number of timers that wait to fire.</summary>
    public int PendingTimers
    {
        get
        {
            lock (_timers)
            {
                return _timers.Count(timer => !timer.IsDisposed);
            }
        }
    }

    /// <summary>Waits until the given number of timers wait to fire, for code that creates its timer after an asynchronous step.</summary>
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
    public override DateTimeOffset GetUtcNow() => _now;

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(callback, state, _now + dueTime);
        lock (_timers)
        {
            _timers.Add(timer);
        }

        return timer;
    }

    /// <summary>Moves the time forward and fires the timers that are due.</summary>
    /// <param name="by">How far to move.</param>
    public void Advance(TimeSpan by)
    {
        _now += by;
        List<ManualTimer> due;
        lock (_timers)
        {
            due = [.. _timers.Where(timer => !timer.IsDisposed && timer.Due <= _now)];
            _timers.RemoveAll(due.Contains);
        }

        foreach (var timer in due)
        {
            timer.Fire();
        }
    }
}
