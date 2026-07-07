using SharedKernel.Testing;

namespace SharedKernel.EntityFrameworkCore.Tests;

[Trait(SharedKernelTestTraitNames.CategoryName, TestTraits.DatabaseIntegrationCategory)]
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.IntegrationEventTransportCapability)]
public sealed class PostgreSqlIntegrationEventTransportTests : IAsyncLifetime
{
    private PostgreSqlIntegrationEventTransportScenario? scenario;

    private PostgreSqlIntegrationEventTransportScenario Scenario =>
        scenario ?? throw new InvalidOperationException("Test scenario is not initialized.");

    public async ValueTask InitializeAsync()
    {
        scenario = await PostgreSqlIntegrationEventTransportScenario.Create(TestContext.Current.CancellationToken);
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
}
