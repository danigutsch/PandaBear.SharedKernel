namespace PandaBear.Blazor.Tests;

/// <summary>A one-shot timer of <see cref="ManualTimeProvider"/>.</summary>
/// <param name="callback">The callback.</param>
/// <param name="state">The callback state.</param>
/// <param name="due">When it fires.</param>
internal sealed class ManualTimer(TimerCallback callback, object? state, DateTimeOffset due) : ITimer
{
    /// <summary>Gets when the timer fires.</summary>
    public DateTimeOffset Due { get; private set; } = due;

    /// <summary>Gets whether the timer was disposed.</summary>
    public bool IsDisposed { get; private set; }

    /// <summary>Runs the callback unless the timer was disposed.</summary>
    public void Fire()
    {
        if (!IsDisposed)
        {
            callback(state);
        }
    }

    /// <inheritdoc />
    public bool Change(TimeSpan dueTime, TimeSpan period)
    {
        Due = DateTimeOffset.MaxValue;
        return !IsDisposed;
    }

    /// <inheritdoc />
    public void Dispose() => IsDisposed = true;

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
