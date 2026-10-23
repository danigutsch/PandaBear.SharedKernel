using PandaBear.VaultManager.Tool.Tests.Pipeline;

namespace PandaBear.VaultManager.Tool.Tests.Cli;

/// <summary>
/// Clock whose timers fire only when the test advances it, so a test controls every timeout without waiting. It reports
/// the first timer it creates, which tells the test that the code under test started a timed wait.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly Lock timersLock = new();
    private readonly List<ManualTimer> timers = [];
    private readonly TaskCompletionSource<TimeSpan> firstTimer = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TimeSpan elapsed;

    /// <summary>Runs each time a timer is created, before the timer is returned.</summary>
    public Action? OnTimerCreated { get; init; }

    /// <summary>Completes with the due time of the first timer created.</summary>
    public Task<TimeSpan> FirstTimer => firstTimer.Task;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow()
    {
        lock (timersLock)
        {
            return PipelineInputs.Now + elapsed;
        }
    }

    /// <inheritdoc/>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(callback, state);
        lock (timersLock)
        {
            timer.DueAt = elapsed + dueTime;
            timers.Add(timer);
        }

        firstTimer.TrySetResult(dueTime);
        OnTimerCreated?.Invoke();
        return timer;
    }

    /// <summary>Moves the clock forward and fires, once, every timer that is then due.</summary>
    /// <param name="time">How far to move the clock.</param>
    public void Advance(TimeSpan time)
    {
        ManualTimer[] due;
        lock (timersLock)
        {
            elapsed += time;
            due = [.. timers.Where(timer => timer.DueAt <= elapsed)];
            timers.RemoveAll(timer => timer.DueAt <= elapsed);
        }

        foreach (ManualTimer timer in due)
        {
            timer.Fire();
        }
    }

    /// <summary>One-shot timer that fires when the clock is advanced past its due time, unless it was disposed.</summary>
    private sealed class ManualTimer(TimerCallback callback, object? state) : ITimer
    {
        private int disposed;

        /// <summary>Time on the manual clock at which the timer fires.</summary>
        public TimeSpan DueAt { get; set; }

        /// <inheritdoc/>
        public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException("The manual clock creates one-shot timers only.");

        /// <summary>Runs the callback unless the timer was disposed.</summary>
        public void Fire()
        {
            if (Volatile.Read(ref disposed) == 0)
            {
                callback(state);
            }
        }

        /// <inheritdoc/>
        public void Dispose() => Interlocked.Exchange(ref disposed, 1);

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
