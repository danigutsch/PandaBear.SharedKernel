namespace PandaBear.Repository.Tests;

/// <summary>A timer that fires once on the thread pool as soon as it is created, or never.</summary>
internal sealed class ImmediateTimer : ITimer
{
    /// <summary>Queues <paramref name="callback"/> with <paramref name="state"/> on the thread pool right away when <paramref name="fire"/> is true, and does nothing otherwise.</summary>
    public ImmediateTimer(TimerCallback callback, object? state, bool fire)
    {
        if (fire)
        {
            ThreadPool.QueueUserWorkItem(_ => callback(state));
        }
    }

    /// <summary>Ignores the new schedule and returns false, because the timer fires at most once at creation.</summary>
    public bool Change(TimeSpan dueTime, TimeSpan period) => false;

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
