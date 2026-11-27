using static SharedKernel.RepoConfig.Tool.ResourceBudgetGuard;

namespace SharedKernel.RepoConfig.Tests;

/// <summary>Exercises an outer budget stop while a nested, separately verified Podman workspace is active.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class ResourceBudgetPodmanCompositionTests
{
    /// <summary>Gets whether the Linux process and Podman fixtures can run.</summary>
    public static bool Linux => OperatingSystem.IsLinux();

    [Theory(SkipUnless = nameof(Linux), Skip = "The outer budget fixture requires Linux.")]
    [InlineData(true, false, false, "Completed")]
    [InlineData(true, true, false, "Retained")]
    [InlineData(true, true, true, "Retained")]
    [InlineData(false, false, false, "None")]
    public async Task Outer_pressure_stop_distinguishes_the_engine_outcome_from_process_exit(
        bool cooperate, bool failCleanup, bool cleanupBeforeStop, string expectedTeardown)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = new ResourceBudgetPodmanCompositionFixture(failCleanup, cleanupBeforeStop);
        var good = new Sample(100, true, 100, 100, "ext4", 0, 0m);
        var high = good with { MemoryPressurePercent = 10m };
        Queue<Sample> samples = new([good, high, high]);
        var limits = new Limits(50, 50) { MaximumMemoryPressurePercent = 5m, ConsecutivePressureSamples = 2 };

        // Act
        var report = await fixture.TripBudget(limits, samples.Dequeue, cooperate, ct);
        var roots = await fixture.ReadVerifiedRoots(ct);

        // Assert
        report.Reason.ShouldBe(StopReason.MemoryPressure);
        report.SampleCount.ShouldBe(3);
        report.ExitCode.ShouldBeNull();
        var cleanup = report.Cleanup.ShouldNotBeNull();
        cleanup.ObservationComplete.ShouldBeTrue();
        cleanup.ObservedProcesses.ShouldBeGreaterThanOrEqualTo(cleanupBeforeStop ? 1 : 2);
        cleanup.RemainingObservedProcesses.ShouldBe(0);
        fixture.SiblingIsRunning.ShouldBeTrue();
        var actualTeardown = cleanup.PodmanTeardown?.ToString() ?? "None";
        actualTeardown.ShouldBe(expectedTeardown);
        var rootsRetained = !cooperate || failCleanup;
        Directory.Exists(roots.Graph).ShouldBe(rootsRetained);
        Directory.Exists(roots.Runtime).ShouldBe(rootsRetained);
        File.Exists(fixture.NextEngineRejectedMarker).ShouldBe(cooperate && !cleanupBeforeStop);
        fixture.HasRetainedCleanupReceipt.ShouldBe(failCleanup);
        File.Exists(fixture.CallerMarker).ShouldBeTrue();
    }

    [Fact(Explicit = true, SkipUnless = nameof(Linux), Skip = "The launched child fixture requires Linux.")]
    public async Task Runs_the_nested_workload_when_launched_by_the_outer_budget_fixture()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var marker = Environment.GetEnvironmentVariable(ResourceBudgetPodmanCompositionFixture.MarkerVariable);

        // Act
        if (marker is null)
        {
            return;
        }
        await ResourceBudgetPodmanCompositionFixture.RunWorker(marker, ct);

        // Assert
        File.Exists(marker).ShouldBeTrue();
    }
}
