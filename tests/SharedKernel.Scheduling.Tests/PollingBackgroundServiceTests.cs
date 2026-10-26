using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace PandaBear.Scheduling.Tests;

public sealed class PollingBackgroundServiceTests
{
    private static readonly TimeSpan ShortInterval = TimeSpan.FromMilliseconds(10);

    [Fact]
    public async Task Drains_batches_until_one_processes_nothing_then_polls_again_after_the_interval()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var service = new ScriptedPollingService(NullLogger.Instance, "drain", ShortInterval, 2, 1, 0, 3, 0);

        // Act
        await service.StartAsync(ct);
        await service.ScriptConsumed.WaitAsync(TimeSpan.FromSeconds(30), ct);
        await service.StopAsync(ct);

        // Assert
        service.BatchCalls.ShouldBeGreaterThanOrEqualTo(5, "the second cycle runs after the first drained to zero");
    }

    [Fact]
    public async Task Stops_calling_batches_after_the_one_that_processes_nothing_until_the_interval_elapses()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var service = new ScriptedPollingService(NullLogger.Instance, "waiting", TimeSpan.FromHours(1), 2, 1, 0);

        // Act
        await service.StartAsync(ct);
        await service.ScriptConsumed.WaitAsync(TimeSpan.FromSeconds(30), ct);
        await service.StopAsync(ct);

        // Assert
        service.BatchCalls.ShouldBe(3, "the cycle drains until a batch processes nothing, then waits a full interval");
    }

    [Fact]
    public async Task Keeps_polling_after_a_batch_fails_and_logs_the_failure_type_only()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var logger = new RecordingLogger();
        using var service = new ScriptedPollingService(
            logger,
            "failing",
            ShortInterval,
            new InvalidOperationException("secret detail"),
            1,
            0);

        // Act
        await service.StartAsync(ct);
        await service.ScriptConsumed.WaitAsync(TimeSpan.FromSeconds(30), ct);
        await service.StopAsync(ct);

        // Assert
        var failure = logger.Entries.ShouldHaveSingleItem(entry => entry.Level == LogLevel.Error);
        failure.Message.ShouldContain("failing", StringComparison.Ordinal);
        failure.Message.ShouldContain(nameof(InvalidOperationException), StringComparison.Ordinal);
        failure.Message.ShouldNotContain("secret detail", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stops_when_the_host_requests_cancellation()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var service = new ScriptedPollingService(NullLogger.Instance, "stopping", TimeSpan.FromMinutes(5), 0);
        await service.StartAsync(ct);
        await service.ScriptConsumed.WaitAsync(TimeSpan.FromSeconds(30), ct);

        // Act
        var stop = service.StopAsync(ct);

        // Assert
        await stop.WaitAsync(TimeSpan.FromSeconds(30), ct);
        var executeTask = service.ExecuteTask.ShouldNotBeNull();
        executeTask.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public void Rejects_a_missing_logger()
    {
        // Arrange
        Action create = () => _ = new ScriptedPollingService(null!, "name", ShortInterval);

        // Act
        var exception = create.ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("logger");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Rejects_a_blank_service_name(string serviceName)
    {
        // Arrange
        Action create = () => _ = new ScriptedPollingService(NullLogger.Instance, serviceName, ShortInterval);

        // Act
        var exception = create.ShouldThrow<ArgumentException>();

        // Assert
        exception.ParamName.ShouldBe("serviceName");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_a_poll_interval_that_is_not_positive(int milliseconds)
    {
        // Arrange
        Action create = () => _ = new ScriptedPollingService(NullLogger.Instance, "name", TimeSpan.FromMilliseconds(milliseconds));

        // Act
        var exception = create.ShouldThrow<ArgumentOutOfRangeException>();

        // Assert
        exception.ParamName.ShouldBe("pollInterval");
    }
}
