using Microsoft.Extensions.Time.Testing;
using Microsoft.Extensions.Logging;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// PostgreSQL tests of integration event transport claiming, duplicate rejection, consuming, retries and poison handling.
/// </summary>
/// <param name="fixture">Shared PostgreSQL fixture that supplies the isolated database.</param>
[Trait(SharedKernelTestTraitNames.CategoryName, TestTraits.DatabaseIntegrationCategory)]
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.IntegrationEventTransportCapability)]
public sealed class PostgreSqlIntegrationEventTransportTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private PostgreSqlIntegrationEventTransportScenario? scenario;

    private PostgreSqlIntegrationEventTransportScenario Scenario =>
        scenario ?? throw new InvalidOperationException("Test scenario is not initialized.");

    /// <summary>
    /// Creates the PostgreSQL transport scenario for the test.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        scenario = await PostgreSqlIntegrationEventTransportScenario.Create(fixture, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Disposes the scenario created for the test, if any.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        var currentScenario = scenario;
        scenario = null;

        if (currentScenario is not null)
        {
            await currentScenario.DisposeAsync();
        }
    }

    [Fact]
    public async Task Concurrent_claims_skip_locked_messages_without_overlap()
    {
        // Arrange
        await Scenario.SeedMessages(5, TestContext.Current.CancellationToken);

        // Act
        var claimed = await Scenario.ClaimConcurrently(TestContext.Current.CancellationToken);

        // Assert
        claimed.Length.ShouldBe(5);
        claimed.Select(message => message.Id).Distinct().Count().ShouldBe(5);
    }

    [Fact]
    public async Task Duplicate_delivery_for_same_consumer_and_event_id_is_rejected_by_inbox_key()
    {
        // Arrange
        await Scenario.StageDuplicateDelivery(TestContext.Current.CancellationToken);

        // Act
        Func<Task> save = () => Scenario.SaveDuplicateDelivery(TestContext.Current.CancellationToken);

        // Assert
        _ = await save.ShouldThrow<Microsoft.EntityFrameworkCore.DbUpdateException>();
    }

    [Fact]
    public async Task Consumer_dispatches_claimed_message_and_marks_it_processed()
    {
        // Arrange
        const string eventId = "consumer-success";
        await Scenario.SeedMessage(eventId, TestContext.Current.CancellationToken);
        var publisher = new RecordingEventEnvelopePublisher();

        // Act
        var consumed = await Scenario.ConsumeWith(publisher, TestContext.Current.CancellationToken);

        // Assert
        consumed.ShouldBe(1);
        publisher.Published.ShouldHaveSingleItem().EventId.ShouldBe(eventId);
        var message = await Scenario.GetMessage(eventId, TestContext.Current.CancellationToken);
        message.ProcessedAt.ShouldNotBeNull();
        message.LastConsumeAttemptAt.ShouldBe(message.ProcessedAt);
        message.LastConsumeError.ShouldBeNull();
        message.NextConsumeAttemptAt.ShouldBeNull();
        message.ClaimedBy.ShouldBeNull();
        message.ClaimedUntil.ShouldBeNull();
    }

    [Fact]
    public async Task Consumer_accepts_a_decorated_unkeyed_application_publisher()
    {
        // Arrange
        const string eventId = "consumer-decorated-application-publisher";
        await Scenario.SeedMessage(eventId, TestContext.Current.CancellationToken);
        var applicationPublisher = new RecordingEventEnvelopePublisher();
        var decoratedPublisher = new DelegatingEventEnvelopePublisher(applicationPublisher);

        // Act
        var consumed = await Scenario.ConsumeWith(decoratedPublisher, TestContext.Current.CancellationToken);

        // Assert
        consumed.ShouldBe(1);
        applicationPublisher.Published.ShouldHaveSingleItem().EventId.ShouldBe(eventId);
        var message = await Scenario.GetMessage(eventId, TestContext.Current.CancellationToken);
        message.ProcessedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Consumer_records_retry_state_when_dispatch_fails()
    {
        // Arrange
        const string eventId = "consumer-failure";
        await Scenario.SeedMessage(eventId, TestContext.Current.CancellationToken);
        var publisher = new RecordingEventEnvelopePublisher
        {
            Failure = new InvalidOperationException("handler failed for jane@example.com")
        };

        // Act
        var consumed = await Scenario.ConsumeWith(publisher, TestContext.Current.CancellationToken);

        // Assert
        consumed.ShouldBe(1);
        publisher.Published.Count.ShouldBe(0);
        var message = await Scenario.GetMessage(eventId, TestContext.Current.CancellationToken);
        message.ProcessedAt.ShouldBeNull();
        message.ConsumeAttempts.ShouldBe(1);
        message.LastConsumeAttemptAt.ShouldNotBeNull();
        message.NextConsumeAttemptAt.ShouldNotBeNull();
        message.LastConsumeError.ShouldBe(typeof(InvalidOperationException).FullName);
        var log = Scenario.LogRecords.ShouldHaveSingleItem();
        log.Level.ShouldBe(LogLevel.Warning);
        log.Exception.ShouldBeSameAs(publisher.Failure);
        log.State.ShouldContain(new KeyValuePair<string, object?>("MessageId", message.Id));
        log.State.ShouldContain(new KeyValuePair<string, object?>("Attempt", 1));
        message.ClaimedBy.ShouldBeNull();
        message.ClaimedUntil.ShouldBeNull();
    }

    [Fact]
    public async Task Consumer_retries_a_fail_once_dispatch_without_losing_the_message()
    {
        // Arrange
        const string eventId = "consumer-fail-once";
        var ct = TestContext.Current.CancellationToken;
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero));
        await Scenario.SeedMessage(eventId, ct);
        var publisher = new RecordingEventEnvelopePublisher
        {
            Failure = new InvalidOperationException("handler is already being processed"),
            FailOnce = true,
        };

        // Act
        var firstConsumed = await Scenario.ConsumeWith(publisher, ct, timeProvider);
        var failedMessage = await Scenario.GetMessage(eventId, ct);
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        var secondConsumed = await Scenario.ConsumeWith(publisher, ct, timeProvider);

        // Assert
        firstConsumed.ShouldBe(1);
        failedMessage.ProcessedAt.ShouldBeNull();
        failedMessage.ConsumeAttempts.ShouldBe(1);
        secondConsumed.ShouldBe(1);
        publisher.Attempts.ShouldBe(2);
        publisher.Published.ShouldHaveSingleItem().EventId.ShouldBe(eventId);
        var processedMessage = await Scenario.GetMessage(eventId, ct);
        processedMessage.ProcessedAt.ShouldNotBeNull();
        processedMessage.LastConsumeError.ShouldBeNull();
    }

    [Fact]
    public async Task Consumer_poisons_a_message_after_its_attempt_limit_and_stops_claiming_it()
    {
        // Arrange
        const string eventId = "consumer-poison";
        var ct = TestContext.Current.CancellationToken;
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero));
        await Scenario.SeedMessage(eventId, ct);
        var publisher = new RecordingEventEnvelopePublisher { Failure = new InvalidOperationException("handler failed") };

        // Act
        var first = await Scenario.ConsumeWith(publisher, ct, timeProvider, maxConsumeAttempts: 2);
        timeProvider.Advance(TimeSpan.FromMinutes(10));
        var second = await Scenario.ConsumeWith(publisher, ct, timeProvider, maxConsumeAttempts: 2);
        timeProvider.Advance(TimeSpan.FromHours(1));
        var third = await Scenario.ConsumeWith(publisher, ct, timeProvider, maxConsumeAttempts: 2);

        // Assert
        (first, second, third).ShouldBe((1, 1, 0));
        var message = await Scenario.GetMessage(eventId, ct);
        message.ConsumeAttempts.ShouldBe(2);
        message.PoisonedAt.ShouldNotBeNull();
        message.LastConsumeError.ShouldBe(typeof(InvalidOperationException).FullName);
        Scenario.LogRecords.Select(record => record.Level).ShouldBe([LogLevel.Warning, LogLevel.Error]);
        message.ProcessedAt.ShouldBeNull();
        message.NextConsumeAttemptAt.ShouldBeNull();
        message.ClaimedBy.ShouldBeNull();
    }

    [Fact]
    public async Task A_poisoned_message_does_not_block_later_messages_and_can_be_replayed()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero));
        await Scenario.SeedMessage("poisoned-first", ct);
        var failing = new RecordingEventEnvelopePublisher { Failure = new InvalidOperationException("handler failed") };
        await Scenario.ConsumeWith(failing, ct, timeProvider, maxConsumeAttempts: 1);
        await Scenario.SeedMessage("healthy-later", ct);
        var healthy = new RecordingEventEnvelopePublisher();

        // Act
        var later = await Scenario.ConsumeWith(healthy, ct, timeProvider, maxConsumeAttempts: 1);
        await Scenario.ReplayPoisoned("poisoned-first", timeProvider.GetUtcNow(), ct);
        var replayed = await Scenario.ConsumeWith(healthy, ct, timeProvider, maxConsumeAttempts: 1);

        // Assert
        (later, replayed).ShouldBe((1, 1));
        healthy.Published.Select(envelope => envelope.EventId).ShouldBe(["healthy-later", "poisoned-first"]);
        var message = await Scenario.GetMessage("poisoned-first", ct);
        message.PoisonedAt.ShouldBeNull();
        message.ProcessedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Consumer_uses_one_scope_and_processes_a_claimed_batch_sequentially()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await Scenario.SeedMessages(2, ct);
        using var publisher = new ControlledEventEnvelopePublisher();

        // Act
        var consumeTask = Scenario.ConsumeBatchWith(publisher, 2, ct).AsTask();
        await publisher.FirstStarted.WaitAsync(ct);
        var invocationCountWhileFirstWasBlocked = publisher.InvocationCount;
        publisher.ReleaseFirst();
        await publisher.SecondStarted.WaitAsync(ct);
        publisher.ReleaseSecond();
        var consumed = await consumeTask;

        // Assert
        invocationCountWhileFirstWasBlocked.ShouldBe(1);
        publisher.InvocationCount.ShouldBe(2);
        publisher.DisposeCount.ShouldBe(1);
        consumed.ShouldBe(2);
    }

    [Fact]
    public async Task Consumer_asynchronously_disposes_an_async_only_scoped_publisher()
    {
        // Arrange
        await Scenario.SeedMessage("consumer-async-disposal", TestContext.Current.CancellationToken);
        var publisher = new AsyncDisposableEventEnvelopePublisher();

        // Act
        var consumed = await Scenario.ConsumeWith(
            publisher,
            TestContext.Current.CancellationToken,
            publisherLifetime: Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped);

        // Assert
        consumed.ShouldBe(1);
        publisher.DisposeCount.ShouldBe(1);
    }
}
