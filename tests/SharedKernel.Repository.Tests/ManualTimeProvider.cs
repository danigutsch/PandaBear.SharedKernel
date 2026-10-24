using System.Collections.Concurrent;

namespace PandaBear.Repository.Tests;

/// <summary>Records every timer and fires one only when the test calls <see cref="Fire"/>, whatever its due time.</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly ConcurrentQueue<(TimerCallback Callback, object? State)> _timers = new();

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        _timers.Enqueue((callback, state));
        return new ImmediateTimer(callback, state, fire: false);
    }

    /// <summary>Fires every timer created so far.</summary>
    public void Fire()
    {
        foreach (var (callback, state) in _timers)
        {
            callback(state);
        }
    }
}
