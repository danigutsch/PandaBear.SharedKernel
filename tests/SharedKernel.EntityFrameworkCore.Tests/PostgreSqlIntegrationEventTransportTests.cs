using SharedKernel.Testing;

namespace SharedKernel.EntityFrameworkCore.Tests;

[Trait(SharedKernelTestTraitNames.CategoryName, TestTraits.DatabaseIntegrationCategory)]
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.IntegrationEventTransportCapability)]
public sealed class PostgreSqlIntegrationEventTransportTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private PostgreSqlIntegrationEventTransportScenario? scenario;

    private PostgreSqlIntegrationEventTransportScenario Scenario =>
        scenario ?? throw new InvalidOperationException("Test scenario is not initialized.");

    public async ValueTask InitializeAsync()
    {
        scenario = await PostgreSqlIntegrationEventTransportScenario.Create(fixture, TestContext.Current.CancellationToken);
    }

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
            Failure = new InvalidOperationException("handler failed")
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
        message.LastConsumeError.ShouldContain("handler failed", StringComparison.Ordinal);
        message.ClaimedBy.ShouldBeNull();
        message.ClaimedUntil.ShouldBeNull();
    }

    [Fact]
    public async Task Consumer_retries_a_fail_once_dispatch_without_losing_the_message()
    {
        // Arrange
        const string eventId = "consumer-fail-once";
        var ct = TestContext.Current.CancellationToken;
        var timeProvider = new AdjustableTimeProvider(new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero));
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
