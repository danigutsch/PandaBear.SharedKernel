namespace PandaBear.Repository.Tests;

/// <summary>A timer that fires once on the thread pool as soon as it is created, or never.</summary>
internal sealed class ImmediateTimer : ITimer
{
    public ImmediateTimer(TimerCallback callback, object? state, bool fire)
    {
        if (fire)
        {
            ThreadPool.QueueUserWorkItem(_ => callback(state));
        }
    }

    public bool Change(TimeSpan dueTime, TimeSpan period) => false;

    public void Dispose()
    {
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
