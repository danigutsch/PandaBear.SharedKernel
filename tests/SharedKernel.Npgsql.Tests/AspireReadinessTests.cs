using Microsoft.Extensions.Diagnostics.HealthChecks;
using PandaBear.IntegrationTesting;

namespace PandaBear.Npgsql.Tests;

/// <summary>
/// Tests the readiness waits of AspireTestApplication with small executables run by Aspire's orchestrator (no container): a resource that is
/// not healthy in time fails with its state and log lines, a state wait ignores health, and an explicitly started resource starts on demand.
/// </summary>
public sealed class AspireReadinessTests
{
    [Fact]
    public async Task A_resource_that_never_becomes_healthy_fails_with_its_state_and_its_log_lines()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var builder = AspireTestApplication.CreateBuilder();
        _ = TalkingProcess.Add(builder, "never-healthy", "ready-line-from-the-resource", healthy: false);
        Func<Task> start = () => AspireTestApplication.Start(builder, ["never-healthy"], TimeSpan.FromSeconds(20), ct);

        // Act
        var timeout = await start.ShouldThrow<TimeoutException>();

        // Assert
        timeout.Message.ShouldContain("never-healthy", StringComparison.Ordinal);
        timeout.Message.ShouldContain("ready-line-from-the-resource", StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_early_startup_failure_keeps_the_typed_cause_and_the_bounded_resource_diagnostics()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder = AspireTestApplication.CreateBuilder();
        TalkingProcess.ModelStartupFailure(builder, stopped);
        Func<Task> start = () => AspireTestApplication.Start(builder, ["modeled-failure"], TimeSpan.FromMinutes(1), ct);

        // Act
        var failure = await start.ShouldThrow<DistributedApplicationException>();

        // Assert
        failure.Message.ShouldContain("modeled-failure: FailedToStart", StringComparison.Ordinal);
        failure.Message.ShouldContain("exit code 17", StringComparison.Ordinal);
        var diagnosticLines = failure.Message.Split('\n');
        diagnosticLines.ShouldContain(line =>
            line.TrimStart().StartsWith("err: ", StringComparison.Ordinal)
            && line.Contains("final-startup-diagnostic", StringComparison.Ordinal));
        var earlierLines = diagnosticLines.Count(line => line.Contains("earlier-startup-progress", StringComparison.Ordinal));
        earlierLines.ShouldBe(49, "the latest 50 lines include the final diagnostic");
        failure.Message.ShouldNotContain("evicted-startup-diagnostic", StringComparison.Ordinal);
        failure.Message.ShouldNotContain("unrelated-resource-diagnostic", StringComparison.Ordinal);
        failure.InnerException.ShouldBeOfType<DistributedApplicationException>()
            .Message.ShouldContain("failed to start", StringComparison.Ordinal);
        stopped.Task.IsCompletedSuccessfully.ShouldBeTrue("the failed application is stopped before startup returns");
    }

    [Fact]
    public async Task Cancelling_the_caller_cancels_the_health_wait()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var builder = AspireTestApplication.CreateBuilder();
        _ = TalkingProcess.Add(builder, "pending-resource", "not-started", healthy: false).WithExplicitStart();
        await using var application = await AspireTestApplication.Start(builder, [], TimeSpan.FromMinutes(1), ct);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // Act
        var waiting = application.WaitForHealthy(["pending-resource"], TimeSpan.FromMinutes(1), caller.Token);
        await caller.CancelAsync();
        Func<Task> wait = () => waiting;
        var cancellation = await wait.ShouldThrowAssignableTo<OperationCanceledException>();

        // Assert
        cancellation.CancellationToken.IsCancellationRequested.ShouldBeTrue("caller cancellation stays cancellation");
    }

    [Fact]
    public async Task An_explicitly_started_resource_starts_on_demand_and_its_logs_can_be_read()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var builder = AspireTestApplication.CreateBuilder();
        _ = TalkingProcess.Add(builder, "on-demand", "hello-from-on-demand", healthy: true).WithExplicitStart();
        await using var application = await AspireTestApplication.Start(builder, [], TimeSpan.FromMinutes(1), ct);

        // Act
        await application.StartResource("on-demand", TimeSpan.FromMinutes(1), ct);

        // Assert
        await Eventually.Succeeds(_ =>
        {
            application.GetResourceLogs("on-demand").ShouldContain(line => line.Contains("hello-from-on-demand", StringComparison.Ordinal));
            return Task.CompletedTask;
        }, TimeSpan.FromSeconds(30), ct);
    }

    [Fact]
    public async Task A_resource_reaches_a_state_without_being_healthy()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var builder = AspireTestApplication.CreateBuilder();
        _ = TalkingProcess.Add(builder, "running-unhealthy", "running-but-never-healthy", healthy: false);
        await using var application = await AspireTestApplication.Start(builder, [], TimeSpan.FromMinutes(1), ct);

        // Act
        var running = await application.WaitForState("running-unhealthy", KnownResourceStates.Running, TimeSpan.FromMinutes(1), ct);

        // Assert
        running.Resource.Name.ShouldBe("running-unhealthy");
        running.Snapshot.State.ShouldNotBeNull().Text.ShouldBe(KnownResourceStates.Running);
        running.Snapshot.HealthStatus.ShouldNotBe(HealthStatus.Healthy, "a state wait must not wait for health");
    }

    [Fact]
    public async Task A_wait_for_the_state_a_resource_is_in_already_returns_without_a_new_transition()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var builder = AspireTestApplication.CreateBuilder();
        _ = TalkingProcess.Add(builder, "already-running", "running-before-the-wait", healthy: true);
        await using var application = await AspireTestApplication.Start(builder, ["already-running"], TimeSpan.FromMinutes(1), ct);

        // Act
        var running = await application.WaitForState("already-running", KnownResourceStates.Running, TimeSpan.FromSeconds(5), ct);

        // Assert
        running.Snapshot.State.ShouldNotBeNull().Text.ShouldBe(KnownResourceStates.Running);
    }

    [Fact]
    public async Task A_resource_that_does_not_reach_a_state_in_time_fails_with_its_current_state()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var builder = AspireTestApplication.CreateBuilder();
        _ = TalkingProcess.Add(builder, "never-started", "never-written", healthy: true).WithExplicitStart();
        await using var application = await AspireTestApplication.Start(builder, [], TimeSpan.FromMinutes(1), ct);
        Func<Task> wait = () => application.WaitForState("never-started", KnownResourceStates.Running, TimeSpan.FromSeconds(5), ct);

        // Act
        var timeout = await wait.ShouldThrow<TimeoutException>();

        // Assert
        timeout.Message.ShouldContain("never-started", StringComparison.Ordinal);
        timeout.Message.ShouldContain(KnownResourceStates.Running, StringComparison.Ordinal);
        timeout.Message.ShouldContain(KnownResourceStates.NotStarted, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_resource_has_no_state_to_wait_for()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var application = await AspireTestApplication.Start(AspireTestApplication.CreateBuilder(), [], TimeSpan.FromMinutes(1), ct);
        Func<Task> wait = () => application.WaitForState("missing", KnownResourceStates.Running, TimeSpan.FromMinutes(1), ct);

        // Act
        var refused = await wait.ShouldThrow<InvalidOperationException>();

        // Assert
        refused.Message.ShouldContain("missing", StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_resource_cannot_be_started_or_waited_for()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var application = await AspireTestApplication.Start(AspireTestApplication.CreateBuilder(), [], TimeSpan.FromMinutes(1), ct);
        Func<Task> start = () => application.StartResource("missing", TimeSpan.FromSeconds(5), ct);

        // Act
        var refused = await start.ShouldThrow<InvalidOperationException>();

        // Assert
        refused.Message.ShouldContain("missing", StringComparison.Ordinal);
    }
}
