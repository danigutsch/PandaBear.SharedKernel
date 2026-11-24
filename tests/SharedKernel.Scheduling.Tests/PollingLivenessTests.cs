using Microsoft.Extensions.Logging.Abstractions;

namespace PandaBear.Scheduling.Tests;

/// <summary>Tests the observable batch duration throughout the lifetime of a polling service.</summary>
public sealed class PollingLivenessTests
{
    private static readonly TimeSpan WaitBound = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);

    [Fact]
    public async Task Exposes_a_stalled_first_batch_before_any_cycle_duration_is_recorded()
    {
        // Arrange
        const string workerName = "first-cycle-worker";
        var ct = TestContext.Current.CancellationToken;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<CancellationToken, ValueTask<int>> batch = async stoppingToken =>
        {
            started.SetResult();
            return await release.Task.WaitAsync(stoppingToken);
        };
        using var recorder = new PollingMeasurementRecorder(workerName);
        using var service = new ScriptedPollingService(NullLogger.Instance, workerName, PollInterval, batch);

        // Act
        await service.StartAsync(ct);
        await started.Task.WaitAsync(WaitBound, ct);
        var measurements = recorder.ObserveBatches();
        var cycleCompleted = recorder.FirstCompletedCycle.IsCompleted;
        await service.StopAsync(ct);

        // Assert
        cycleCompleted.ShouldBeFalse();
        var (duration, unit, dimensions) = measurements.ShouldHaveSingleItem();
        duration.ShouldBeGreaterThan(0);
        unit.ShouldBe("s");
        dimensions.ShouldBe([new KeyValuePair<string, object?>("sharedkernel.scheduling.service.name", workerName)]);
    }

    [Fact]
    public async Task Reports_zero_while_idle_and_stops_observing_after_the_worker_exits()
    {
        // Arrange
        const string workerName = "idle-cycle-worker";
        var ct = TestContext.Current.CancellationToken;
        using var recorder = new PollingMeasurementRecorder(workerName);
        using var service = new ScriptedPollingService(NullLogger.Instance, workerName, PollInterval, 0);

        // Act
        var beforeStart = recorder.ObserveBatches();
        await service.StartAsync(ct);
        await recorder.FirstCompletedCycle.WaitAsync(WaitBound, ct);
        var idle = recorder.ObserveBatches();
        await service.StopAsync(ct);
        var stopped = recorder.ObserveBatches();

        // Assert
        beforeStart.ShouldBeEmpty();
        var (duration, _, _) = idle.ShouldHaveSingleItem();
        duration.ShouldBe(0);
        stopped.ShouldBeEmpty();
    }

    [Fact]
    public async Task Uses_one_observation_for_workers_with_the_same_stable_name()
    {
        // Arrange
        const string workerName = "duplicate-name-worker";
        var ct = TestContext.Current.CancellationToken;
        using var started = new SemaphoreSlim(0);
        var release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<CancellationToken, ValueTask<int>> batch = stoppingToken =>
        {
            started.Release();
            return new ValueTask<int>(release.Task.WaitAsync(stoppingToken));
        };
        using var recorder = new PollingMeasurementRecorder(workerName);
        using var first = new ScriptedPollingService(NullLogger.Instance, workerName, PollInterval, batch);
        using var second = new ScriptedPollingService(NullLogger.Instance, workerName, PollInterval, batch);

        // Act
        await first.StartAsync(ct);
        await second.StartAsync(ct);
        var oneStarted = await started.WaitAsync(WaitBound, ct);
        var bothStarted = await started.WaitAsync(WaitBound, ct);
        var active = recorder.ObserveBatches();
        await first.StopAsync(ct);
        var remaining = recorder.ObserveBatches();
        await second.StopAsync(ct);

        // Assert
        oneStarted.ShouldBeTrue();
        bothStarted.ShouldBeTrue();
        active.ShouldHaveSingleItem();
        remaining.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Stops_observing_when_shutdown_begins_before_an_uncooperative_batch_exits()
    {
        // Arrange
        const string workerName = "shutdown-cycle-worker";
        var ct = TestContext.Current.CancellationToken;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<CancellationToken, ValueTask<int>> batch = _ =>
        {
            started.SetResult();
            return new ValueTask<int>(release.Task);
        };
        using var recorder = new PollingMeasurementRecorder(workerName);
        using var service = new ScriptedPollingService(NullLogger.Instance, workerName, PollInterval, batch);

        // Act
        await service.StartAsync(ct);
        await started.Task.WaitAsync(WaitBound, ct);
        var active = recorder.ObserveBatches();
        var stop = service.StopAsync(ct);
        var stopping = recorder.ObserveBatches();
        release.SetResult(0);
        await stop.WaitAsync(WaitBound, ct);

        // Assert
        active.ShouldHaveSingleItem();
        stopping.ShouldBeEmpty();
    }
}
