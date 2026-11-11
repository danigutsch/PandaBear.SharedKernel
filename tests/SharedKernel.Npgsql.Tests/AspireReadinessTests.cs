using PandaBear.IntegrationTesting;

namespace PandaBear.Npgsql.Tests;

/// <summary>
/// Tests the readiness waits of AspireTestApplication with small executables run by Aspire's orchestrator (no container): a resource that is
/// not healthy in time fails with its state and log lines, and an explicitly started resource starts on demand.
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
