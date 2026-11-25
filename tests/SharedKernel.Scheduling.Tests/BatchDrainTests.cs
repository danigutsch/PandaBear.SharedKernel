namespace SharedKernel.Scheduling.Tests;

/// <summary>Verifies the telemetry and lifetime of a caller-owned batch drain.</summary>
public sealed class BatchDrainTests
{
    private static readonly TimeSpan WaitBound = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Observes_a_blocked_first_batch_then_records_items_and_the_final_empty_batch()
    {
        // Arrange
        const string serviceName = "caller-owned-success";
        var ct = TestContext.Current.CancellationToken;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tokens = new List<CancellationToken>();
        var batch = new Func<CancellationToken, ValueTask<int>>(token =>
        {
            tokens.Add(token);
            if (tokens.Count != 1)
            {
                return ValueTask.FromResult(0);
            }

            started.SetResult();
            return new ValueTask<int>(release.Task.WaitAsync(token));
        });
        using var recorder = new PollingMeasurementRecorder(serviceName);
        using var capture = new SchedulingTelemetryCapture(serviceName);
        var beforeRegistration = recorder.ObserveBatches();
        using var drain = new BatchDrain(serviceName);

        // Act
        var beforeRun = recorder.ObserveBatches();
        var run = drain.Run(batch, ct).AsTask();
        await started.Task.WaitAsync(WaitBound, ct);
        var active = recorder.ObserveBatches();
        var cycleCompleted = recorder.FirstCompletedCycle.IsCompleted;
        release.SetResult(3);
        await run.WaitAsync(WaitBound, ct);
        var afterRun = recorder.ObserveBatches();

        // Assert
        beforeRegistration.ShouldBeEmpty();
        beforeRun.ShouldHaveSingleItem().Duration.ShouldBe(0);
        var observation = active.ShouldHaveSingleItem();
        observation.Duration.ShouldBeGreaterThan(0);
        observation.Unit.ShouldBe("s");
        observation.Tags.ShouldBe([new KeyValuePair<string, object?>("sharedkernel.scheduling.service.name", serviceName)]);
        cycleCompleted.ShouldBeFalse();
        tokens.ShouldBe([ct, ct]);
        afterRun.ShouldHaveSingleItem().Duration.ShouldBe(0);
        var span = capture.Activities.ShouldHaveSingleItem();
        span.OperationName.ShouldBe("scheduling.poll");
        span.GetTagItem(SchedulingTelemetry.TagOutcome).ShouldBe("success");
        span.GetTagItem(SchedulingTelemetry.TagItemCount).ShouldBe(3);
        capture.Measurements.ShouldHaveSingleItem(static measurement => measurement.Instrument == SchedulingTelemetry.MetricPollingCycleDuration)
            .Tags.GetValueOrDefault(SchedulingTelemetry.TagOutcome).ShouldBe("success");
        capture.Measurements.Count(static measurement => measurement.Instrument == SchedulingTelemetry.MetricPollingBatches).ShouldBe(2);
        capture.Measurements.ShouldHaveSingleItem(static measurement => measurement.Instrument == SchedulingTelemetry.MetricPollingItems).Value.ShouldBe(3);
    }

    [Fact]
    public async Task Cancellation_propagates_and_clears_active_age_without_counting_a_failed_batch()
    {
        // Arrange
        const string serviceName = "caller-owned-cancelled";
        var ct = TestContext.Current.CancellationToken;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var batch = new Func<CancellationToken, ValueTask<int>>(token =>
        {
            started.SetResult();
            return new ValueTask<int>(release.Task.WaitAsync(token));
        });
        using var recorder = new PollingMeasurementRecorder(serviceName);
        using var capture = new SchedulingTelemetryCapture(serviceName);
        using var drain = new BatchDrain(serviceName);

        // Act
        var run = drain.Run(batch, cancellation.Token).AsTask();
        await started.Task.WaitAsync(WaitBound, ct);
        await cancellation.CancelAsync();
        var exception = await new Func<Task>(() => run.WaitAsync(WaitBound, ct)).ShouldThrowAssignableTo<OperationCanceledException>();
        var afterCancellation = recorder.ObserveBatches();

        // Assert
        exception.CancellationToken.ShouldBe(cancellation.Token);
        afterCancellation.ShouldHaveSingleItem().Duration.ShouldBe(0);
        var span = capture.Activities.ShouldHaveSingleItem();
        span.GetTagItem(SchedulingTelemetry.TagOutcome).ShouldBe("cancelled");
        span.GetTagItem(SchedulingTelemetry.TagErrorType).ShouldBeNull();
        var duration = capture.Measurements.ShouldHaveSingleItem();
        duration.Instrument.ShouldBe(SchedulingTelemetry.MetricPollingCycleDuration);
        duration.Tags.GetValueOrDefault(SchedulingTelemetry.TagOutcome).ShouldBe("cancelled");
    }

    [Fact]
    public async Task Failure_propagates_and_clears_active_age_with_only_bounded_error_metadata()
    {
        // Arrange
        const string serviceName = "caller-owned-error";
        var ct = TestContext.Current.CancellationToken;
        var failure = new InvalidOperationException("private document context");
        using var recorder = new PollingMeasurementRecorder(serviceName);
        using var capture = new SchedulingTelemetryCapture(serviceName);
        using var drain = new BatchDrain(serviceName);
        var run = new Func<Task>(() => drain.Run(_ => ValueTask.FromException<int>(failure), ct).AsTask());

        // Act
        var exception = await run.ShouldThrow<InvalidOperationException>();
        var afterFailure = recorder.ObserveBatches();

        // Assert
        exception.ShouldBeSameAs(failure);
        afterFailure.ShouldHaveSingleItem().Duration.ShouldBe(0);
        var span = capture.Activities.ShouldHaveSingleItem();
        span.GetTagItem(SchedulingTelemetry.TagOutcome).ShouldBe("error");
        span.GetTagItem(SchedulingTelemetry.TagErrorType).ShouldBe(typeof(InvalidOperationException).FullName);
        span.StatusDescription.ShouldBeNull();
        var duration = capture.Measurements.ShouldHaveSingleItem();
        duration.Instrument.ShouldBe(SchedulingTelemetry.MetricPollingCycleDuration);
        duration.Tags.GetValueOrDefault(SchedulingTelemetry.TagOutcome).ShouldBe("error");
    }

    [Fact]
    public async Task Disposal_removes_only_its_registration_without_stopping_the_running_batch()
    {
        // Arrange
        const string serviceName = "caller-owned-disposal";
        var ct = TestContext.Current.CancellationToken;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var batch = new Func<CancellationToken, ValueTask<int>>(token =>
        {
            started.SetResult();
            return new ValueTask<int>(release.Task.WaitAsync(token));
        });
        using var recorder = new PollingMeasurementRecorder(serviceName);
        using var activeOwner = new BatchDrain(serviceName);
        using var idleOwner = new BatchDrain(serviceName);

        // Act
        var run = activeOwner.Run(batch, ct).AsTask();
        await started.Task.WaitAsync(WaitBound, ct);
        await new Func<Task>(() => activeOwner.Run(batch, ct).AsTask()).ShouldThrow<InvalidOperationException>();
        var combined = recorder.ObserveBatches();
        activeOwner.Dispose();
        var remaining = recorder.ObserveBatches();
        var runCompletedAtDisposal = run.IsCompleted;
        release.SetResult(0);
        await run.WaitAsync(WaitBound, ct);
        idleOwner.Dispose();
        var removed = recorder.ObserveBatches();
        await new Func<Task>(() => activeOwner.Run(batch, ct).AsTask()).ShouldThrow<ObjectDisposedException>();

        // Assert
        combined.ShouldHaveSingleItem().Duration.ShouldBeGreaterThan(0);
        remaining.ShouldHaveSingleItem().Duration.ShouldBe(0);
        runCompletedAtDisposal.ShouldBeFalse();
        removed.ShouldBeEmpty();
    }
}
