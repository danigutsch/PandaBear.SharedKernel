using SharedKernel.Testing;

namespace SharedKernel.EntityFrameworkCore.Tests;

[Trait(SharedKernelTestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.IntegrationEventTransportCapability)]
public sealed class ContextQualifiedMessagingRegistrationTests
{
    [Fact]
    public async Task Context_qualified_outboxes_stage_events_in_their_own_contexts()
    {
        // Arrange
        await using var scenario = ContextQualifiedMessagingScenario.Create();

        // Act
        await scenario.EnqueueInEachContext(TestContext.Current.CancellationToken);

        // Assert
        var counts = await scenario.CountOutboxMessages(TestContext.Current.CancellationToken);
        counts.First.ShouldBe(1);
        counts.Second.ShouldBe(1);
    }

    [Fact]
    public async Task Context_qualified_outboxes_use_their_own_serializers()
    {
        // Arrange
        await using var scenario = ContextQualifiedMessagingScenario.Create();

        // Act
        await scenario.EnqueueInEachContext(TestContext.Current.CancellationToken);
        var payloads = await scenario.GetSingleOutboxPayloads(TestContext.Current.CancellationToken);

        // Assert
        payloads.First.ShouldBe(ContextQualifiedMessagingTestIntegrationEventSerializer.FirstPayload);
        payloads.Second.ShouldBe(ContextQualifiedMessagingTestIntegrationEventSerializer.SecondPayload);
    }

    [Fact]
    public async Task Domain_event_outbox_uses_the_current_save_context()
    {
        // Arrange
        await using var scenario = ContextQualifiedMessagingScenario.Create();

        // Act
        await scenario.EnqueueDomainEventInSecondContext(TestContext.Current.CancellationToken);

        // Assert
        var counts = await scenario.CountOutboxMessages(TestContext.Current.CancellationToken);
        var secondPayload = await scenario.GetSecondOutboxPayload(TestContext.Current.CancellationToken);
        counts.First.ShouldBe(0);
        counts.Second.ShouldBe(1);
        secondPayload.ShouldBe(ContextQualifiedMessagingTestIntegrationEventSerializer.SecondPayload);
    }

    [Fact]
    public async Task Context_qualified_idempotency_stores_use_their_own_contexts()
    {
        // Arrange
        await using var scenario = ContextQualifiedMessagingScenario.Create();

        // Act
        await scenario.StartIdempotentOperationsInEachContext(TestContext.Current.CancellationToken);

        // Assert
        var counts = await scenario.CountIdempotencyEntries(TestContext.Current.CancellationToken);
        counts.First.ShouldBe(1);
        counts.Second.ShouldBe(1);
    }

    [Fact]
    public async Task Context_qualified_transport_publishers_keep_destinations_isolated()
    {
        // Arrange
        await using var scenario = ContextQualifiedMessagingScenario.Create();

        // Act
        await scenario.PublishTransportMessagesInEachContext(TestContext.Current.CancellationToken);

        // Assert
        var destinations = await scenario.GetTransportDestinations(TestContext.Current.CancellationToken);
        destinations.First.ShouldBe("first-consumer");
        destinations.Second.ShouldBe("second-consumer");
    }

    [Fact]
    public async Task Transport_producers_do_not_replace_an_existing_application_publisher()
    {
        // Arrange
        await using var scenario = ContextQualifiedMessagingScenario.Create();

        // Act
        var published = await scenario.PublishThroughApplicationPublisher(TestContext.Current.CancellationToken);

        // Assert
        published.ApplicationPublished.ShouldBe(1);
        published.FirstTransport.ShouldBe(0);
        published.SecondTransport.ShouldBe(0);
    }

    [Fact]
    public async Task Application_try_add_after_a_transport_producer_remains_the_unkeyed_publisher()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        var published = await ContextQualifiedMessagingScenario.PublishWithApplicationRegisteredAfterProducer(ct);

        // Assert
        published.ApplicationPublished.ShouldBe(1);
        published.TransportCount.ShouldBe(0);
    }

    [Fact]
    public async Task Transport_consumers_use_their_own_context_qualified_options()
    {
        // Arrange
        await using var scenario = ContextQualifiedMessagingScenario.Create();

        // Act
        var batchSizes = scenario.GetTransportConsumerBatchSizes();

        // Assert
        batchSizes.First.ShouldBe(3);
        batchSizes.Second.ShouldBe(4);
    }

    [Fact]
    public async Task Relays_publish_only_their_own_contexts_events_and_destinations()
    {
        // Arrange
        await using var scenario = ContextQualifiedMessagingScenario.Create();

        // Act
        var publishedEventIds = await scenario.PublishEachOutboxThroughItsRelay(TestContext.Current.CancellationToken);

        // Assert
        var destinations = await scenario.GetTransportDestinations(TestContext.Current.CancellationToken);
        var transportedEventIds = await scenario.GetTransportEventIds(TestContext.Current.CancellationToken);
        destinations.First.ShouldBe("first-consumer");
        destinations.Second.ShouldBe("second-consumer");
        transportedEventIds.First.ShouldBe(publishedEventIds.First.ToString("D"));
        transportedEventIds.Second.ShouldBe(publishedEventIds.Second.ToString("D"));
        scenario.GetApplicationPublishedCount().ShouldBe(0);
    }

    [Fact]
    public async Task Relay_uses_the_unkeyed_application_publisher_when_its_context_key_is_absent()
    {
        // Arrange
        await using var scenario = ContextQualifiedMessagingScenario.CreateWithoutTransportProducers();
        await scenario.EnqueueInFirstContext(TestContext.Current.CancellationToken);

        // Act
        var published = await scenario.PublishFirstRelay(TestContext.Current.CancellationToken);

        // Assert
        published.ShouldBe(1);
        scenario.GetApplicationPublishedCount().ShouldBe(1);
        var state = await scenario.GetFirstOutboxState(TestContext.Current.CancellationToken);
        state.PublishedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Relays_use_their_own_context_qualified_batch_options()
    {
        // Arrange
        await using var scenario = ContextQualifiedMessagingScenario.Create();

        // Act
        var published = await scenario.PublishUsingContextRelayOptions(TestContext.Current.CancellationToken);

        // Assert
        published.First.ShouldBe(1);
        published.Second.ShouldBe(2);
    }

    [Fact]
    public async Task Relay_records_retry_state_and_disposes_its_context_publisher()
    {
        // Arrange
        var publisher = new DisposableEventEnvelopePublisher
        {
            Failure = new InvalidOperationException("first transport unavailable"),
        };
        await using var scenario = ContextQualifiedMessagingScenario.CreateWithFirstPublisher(publisher);
        await scenario.EnqueueInFirstContext(TestContext.Current.CancellationToken);

        // Act
        var claimed = await scenario.PublishFirstRelay(TestContext.Current.CancellationToken);

        // Assert
        claimed.ShouldBe(1);
        var state = await scenario.GetFirstOutboxState(TestContext.Current.CancellationToken);
        state.PublishedAt.ShouldBeNull();
        state.Attempts.ShouldBe(1);
        state.LastError.ShouldContain("first transport unavailable", StringComparison.Ordinal);
        state.ClaimedBy.ShouldBeNull();
        state.ClaimedUntil.ShouldBeNull();
        publisher.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task Relay_propagates_cancellation_without_recording_a_retry_and_disposes_its_publisher()
    {
        // Arrange
        var publisher = new DisposableEventEnvelopePublisher();
        await using var scenario = ContextQualifiedMessagingScenario.CreateWithFirstPublisher(publisher);
        await scenario.EnqueueInFirstContext(TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        Func<Task> publish = async () => _ = await scenario.PublishFirstRelay(cancellation.Token);

        // Assert
        _ = await publish.ShouldThrow<OperationCanceledException>();
        var state = await scenario.GetFirstOutboxState(TestContext.Current.CancellationToken);
        state.PublishedAt.ShouldBeNull();
        state.Attempts.ShouldBe(0);
        state.LastError.ShouldBeNull();
        state.ClaimedBy.ShouldBeNull();
        state.ClaimedUntil.ShouldBeNull();
        publisher.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task Relay_asynchronously_disposes_an_async_only_context_publisher()
    {
        // Arrange
        var publisher = new AsyncDisposableEventEnvelopePublisher();
        await using var scenario = ContextQualifiedMessagingScenario.CreateWithFirstPublisher(publisher);
        await scenario.EnqueueInFirstContext(TestContext.Current.CancellationToken);

        // Act
        var published = await scenario.PublishFirstRelay(TestContext.Current.CancellationToken);

        // Assert
        published.ShouldBe(1);
        publisher.DisposeCount.ShouldBe(1);
    }
}
