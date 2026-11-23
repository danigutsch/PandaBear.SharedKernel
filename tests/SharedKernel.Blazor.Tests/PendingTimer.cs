namespace PandaBear.Blazor.Tests;

/// <summary>A timer of <see cref="ManualTimeProvider"/> that remembers whether it fired or was disposed.</summary>
internal sealed class PendingTimer(DateTimeOffset due) : ITimer
{
    private int _fired;
    private int _disposed;

    /// <summary>Gets when the timer fires, as it was created: the time then plus its due time.</summary>
    public DateTimeOffset Due { get; } = due;

    /// <summary>Gets or sets the <see cref="Microsoft.Extensions.Time.Testing.FakeTimeProvider"/> timer that does the work.</summary>
    public ITimer? Inner { get; set; }

    /// <summary>Gets whether the timer has neither fired nor been disposed.</summary>
    public bool IsPending => Volatile.Read(ref _fired) == 0 && Volatile.Read(ref _disposed) == 0;

    /// <summary>Records that the timer fired.</summary>
    public void MarkFired() => Interlocked.Exchange(ref _fired, 1);

    /// <inheritdoc />
    public bool Change(TimeSpan dueTime, TimeSpan period) => Inner!.Change(dueTime, period);

    /// <inheritdoc />
    public void Dispose()
    {
        Interlocked.Exchange(ref _disposed, 1);
        Inner?.Dispose();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
