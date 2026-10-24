using System.Collections.Concurrent;

namespace PandaBear.Repository.Tests;

/// <summary>
/// Records every timer and fires at once each one due within <paramref name="fireWithin"/>; later timers never fire.
/// Retry waits then take no time while long request timeouts stay pending, or every timeout fires at once.
/// </summary>
internal sealed class ImmediateTimeProvider(TimeSpan fireWithin) : TimeProvider
{
    private readonly ConcurrentQueue<TimeSpan> _timers = new();

    /// <summary>Gets the due time of every timer created so far, in order.</summary>
    public TimeSpan[] Timers => [.. _timers];

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        _timers.Enqueue(dueTime);
        return new ImmediateTimer(callback, state, dueTime >= TimeSpan.Zero && dueTime <= fireWithin);
    }
}
