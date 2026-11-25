using static SharedKernel.RepoConfig.Tool.ResourceBudgetGuard;

namespace SharedKernel.RepoConfig.Tests;

/// <summary>Exercises the opt-in process budget without putting pressure on the test host.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class ResourceBudgetGuardTests
{
    [Theory]
    [InlineData(null, true, null, null, 100, "ext4", false, nameof(StopReason.MeasurementUnavailable))]
    [InlineData(100, false, null, 50, 100, "ext4", false, nameof(StopReason.MeasurementUnavailable))]
    [InlineData(100, true, null, 50, 100, "ext4", false, nameof(StopReason.MeasurementUnavailable))]
    [InlineData(100, true, null, null, null, "ext4", false, nameof(StopReason.MeasurementUnavailable))]
    [InlineData(49, true, null, null, 100, "ext4", false, nameof(StopReason.HostMemoryFloor))]
    [InlineData(100, true, 49, 50, 100, "ext4", false, nameof(StopReason.CgroupMemoryFloor))]
    [InlineData(100, true, null, null, 49, "ext4", false, nameof(StopReason.ScratchSpaceFloor))]
    [InlineData(100, true, null, null, 100, null, true, nameof(StopReason.MeasurementUnavailable))]
    [InlineData(100, true, null, null, 100, "tmpfs", true, nameof(StopReason.ScratchFilesystem))]
    [InlineData(100, true, null, null, 100, "ramfs", true, nameof(StopReason.ScratchFilesystem))]
    public async Task Refuses_to_start_when_a_required_preflight_budget_fails(
        int? host, bool cgroupMeasured, int? headroom, int? cgroupFloor, int? scratch, string? filesystem, bool rejectTmpfs, string expected)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var sample = new Sample(host, cgroupMeasured, headroom, scratch, filesystem, ShmemBytes: 5, MemoryPressurePercent: null);
        var limits = new Limits(50, 50, cgroupFloor) { RejectMemoryBackedScratch = rejectTmpfs };
        var started = false;

        // Act
        var report = await Run(limits, () => sample, static _ => Task.CompletedTask, _ =>
        {
            started = true;
            return Task.FromResult(new ChildResult(0));
        }, ct);

        // Assert
        started.ShouldBeFalse();
        report.Reason.ToString().ShouldBe(expected);
        report.InitialSample.ShouldBe(sample);
        report.LastSample.ShouldBe(sample);
        report.SampleCount.ShouldBe(1);
    }

    [Theory]
    [InlineData(true, 5, 5)]
    [InlineData(true, null, null)]
    [InlineData(false, null, null)]
    public async Task Preserves_the_exit_status_at_inclusive_floors_and_permits_caller_selected_tmpfs(
        bool cgroupMeasured, int? cgroupHeadroom, int? cgroupFloor)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var sample = new Sample(50, cgroupMeasured, cgroupHeadroom, 50, "tmpfs", 9, null);
        var waited = false;

        // Act
        var report = await Run(new Limits(50, 50, cgroupFloor), () => sample, _ =>
        {
            waited = true;
            return Task.CompletedTask;
        }, static _ => Task.FromResult(new ChildResult(23)), ct);

        // Assert
        waited.ShouldBeFalse();
        report.Reason.ShouldBe(StopReason.None);
        report.ExitCode.ShouldBe(23);
        report.SampleCount.ShouldBe(1);
    }

    [Fact]
    public async Task Stops_the_child_once_a_periodic_sample_crosses_the_filesystem_floor()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var good = new Sample(100, true, 100, 105, "ext4", 12, null);
        var low = good with { ScratchAvailableBytes = 49 };
        Queue<Sample> samples = new([good, low]);
        using var child = new ResourceBudgetTestChild();

        // Act
        var report = await Run(new Limits(50, 50), samples.Dequeue, static _ => Task.CompletedTask, child.Run, ct);

        // Assert
        report.Reason.ShouldBe(StopReason.ScratchSpaceFloor);
        report.ExitCode.ShouldBeNull();
        report.InitialSample.ShouldBe(good);
        report.LastSample.ShouldBe(low);
        report.SampleCount.ShouldBe(2);
        report.Cleanup.ShouldBe(new Cleanup(true, 1, 0));
        child.Stopped.ShouldBeTrue();
    }

    [Theory]
    [InlineData(1, 5, true)]
    [InlineData(1, 2, false)]
    [InlineData(2, 5, false)]
    public async Task Declines_launch_only_when_preflight_completes_the_configured_pressure_streak(
        int consecutiveSamples, int pressure, bool shouldStop)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var sample = new Sample(100, true, null, 100, "ext4", 9, pressure);
        var limits = new Limits(50, 50) { MaximumMemoryPressurePercent = 2m, ConsecutivePressureSamples = consecutiveSamples };
        var started = false;

        // Act
        var report = await Run(limits, () => sample, static _ => Task.CompletedTask, _ =>
        {
            started = true;
            return Task.FromResult(new ChildResult(27));
        }, ct);

        // Assert
        started.ShouldBe(!shouldStop);
        report.Started.ShouldBe(!shouldStop);
        report.Reason.ShouldBe(shouldStop ? StopReason.MemoryPressure : StopReason.None);
        report.ExitCode.ShouldBe(shouldStop ? null : 27);
        report.SampleCount.ShouldBe(1);
    }

    [Fact]
    public async Task Requires_consecutive_pressure_samples_and_resets_at_the_limit()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var good = new Sample(100, true, null, 100, "ext4", 9, 0m);
        var high = good with { MemoryPressurePercent = 5m };
        var atLimit = good with { MemoryPressurePercent = 2m };
        Queue<Sample> samples = new([good, high, atLimit, high, high]);
        var limits = new Limits(50, 50) { MaximumMemoryPressurePercent = 2m, ConsecutivePressureSamples = 2 };
        using var child = new ResourceBudgetTestChild();

        // Act
        var report = await Run(limits, samples.Dequeue, static _ => Task.CompletedTask, child.Run, ct);

        // Assert
        report.Reason.ShouldBe(StopReason.MemoryPressure);
        report.SampleCount.ShouldBe(5);
        report.LastSample.ShouldBe(high);
        child.Stopped.ShouldBeTrue();
    }

    [Fact]
    public async Task Stops_when_an_opted_in_pressure_measurement_disappears()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var good = new Sample(100, true, null, 100, "ext4", 9, 0m);
        Queue<Sample> samples = new([good, good with { MemoryPressurePercent = null }]);
        using var child = new ResourceBudgetTestChild();

        // Act
        var report = await Run(new Limits(50, 50) { MaximumMemoryPressurePercent = 2m },
            samples.Dequeue, static _ => Task.CompletedTask, child.Run, ct);

        // Assert
        report.Reason.ShouldBe(StopReason.MeasurementUnavailable);
        report.SampleCount.ShouldBe(2);
        child.Stopped.ShouldBeTrue();
    }

    [Fact]
    public async Task Records_caller_cancellation_and_observed_cleanup()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var child = new ResourceBudgetTestChild();
        var good = new Sample(100, true, null, 100, "ext4", 9, null);

        // Act
        var report = await Run(new Limits(50, 50), () => good, _ => cancellation.CancelAsync(), child.Run, cancellation.Token);

        // Assert
        report.Reason.ShouldBe(StopReason.Cancelled);
        report.Cleanup.ShouldBe(new Cleanup(true, 1, 0));
        report.SampleCount.ShouldBe(1);
    }

    [Fact]
    public async Task Already_cancelled_caller_never_starts_the_command()
    {
        // Arrange
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        var good = new Sample(100, true, 75, 100, "ext4", 9, 0m);
        var launched = false;

        // Act
        var report = await Run(new Limits(50, 50, 25), () => good, static _ => Task.CompletedTask, _ =>
        {
            launched = true;
            return Task.FromResult(new ChildResult(0));
        }, cancellation.Token);

        // Assert
        launched.ShouldBeFalse();
        report.Started.ShouldBeFalse();
        report.Reason.ShouldBe(StopReason.Cancelled);
        report.Cleanup.ShouldBeNull();
    }

    [Fact]
    public async Task Filesystem_restriction_permits_reported_disk_backed_scratch()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var good = new Sample(100, true, 75, 100, "ext4", 9, 0m);
        var limits = new Limits(50, 50) { RejectMemoryBackedScratch = true };

        // Act
        var report = await Run(limits, () => good, static _ => Task.CompletedTask,
            static _ => Task.FromResult(new ChildResult(0)), ct);

        // Assert
        report.Started.ShouldBeTrue();
        report.Reason.ShouldBe(StopReason.None);
        report.ExitCode.ShouldBe(0);
    }
}
