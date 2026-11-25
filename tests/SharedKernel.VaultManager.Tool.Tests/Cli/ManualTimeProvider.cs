using Microsoft.Extensions.Time.Testing;
using SharedKernel.VaultManager.Tool.Tests.Pipeline;

namespace SharedKernel.VaultManager.Tool.Tests.Cli;

/// <summary>
/// A <see cref="FakeTimeProvider"/> that starts at <see cref="PipelineInputs.Now"/> and reports the first timer it creates,
/// which tells the test that the code under test started a timed wait.
/// </summary>
internal sealed class ManualTimeProvider() : FakeTimeProvider(PipelineInputs.Now)
{
    private readonly TaskCompletionSource<TimeSpan> firstTimer = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Runs each time a timer is created, before the timer is returned.</summary>
    public Action? OnTimerCreated { get; init; }

    /// <summary>Completes with the due time of the first timer created.</summary>
    public Task<TimeSpan> FirstTimer => firstTimer.Task;

    /// <inheritdoc/>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = base.CreateTimer(callback, state, dueTime, period);
        firstTimer.TrySetResult(dueTime);
        OnTimerCreated?.Invoke();
        return timer;
    }
}
