using Microsoft.EntityFrameworkCore;
using SharedKernel.Testing;

namespace SharedKernel.EntityFrameworkCore.Tests;

[Trait(SharedKernelTestTraitNames.CategoryName, TestTraits.DatabaseIntegrationCategory)]
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.IntegrationEventTransportCapability)]
public sealed class ContextQualifiedMessagingPostgreSqlTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private ContextQualifiedMessagingPostgreSqlScenario? scenario;

    private ContextQualifiedMessagingPostgreSqlScenario Scenario =>
        scenario ?? throw new InvalidOperationException("Test scenario is not initialized.");

    public async ValueTask InitializeAsync()
    {
        scenario = await ContextQualifiedMessagingPostgreSqlScenario.Create(fixture, TestContext.Current.CancellationToken);
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
    public async Task Fresh_context_tables_and_transactions_remain_independent_when_one_outbox_write_fails()
    {
        // Arrange
        Scenario.FirstCreateScript.ShouldContain("first_messaging", StringComparison.Ordinal);
        Scenario.FirstCreateScript.ShouldContain("first_outbox", StringComparison.Ordinal);
        Scenario.FirstCreateScript.ShouldNotContain("second_messaging", StringComparison.Ordinal);
        Scenario.SecondCreateScript.ShouldContain("second_messaging", StringComparison.Ordinal);
        Scenario.SecondCreateScript.ShouldContain("second_outbox", StringComparison.Ordinal);
        Scenario.SecondCreateScript.ShouldNotContain("first_messaging", StringComparison.Ordinal);

        // Act
        Func<Task> saveFirst = () => Scenario.SaveFirstBusinessRecordWithDuplicateOutboxEvent(TestContext.Current.CancellationToken);
        _ = await saveFirst.ShouldThrow<DbUpdateException>();
        await Scenario.CommitSecondBusinessRecordAndOutbox(TestContext.Current.CancellationToken);

        // Assert
        var counts = await Scenario.CountRecords(TestContext.Current.CancellationToken);
        counts.FirstBusiness.ShouldBe(0);
        counts.FirstOutbox.ShouldBe(0);
        counts.SecondBusiness.ShouldBe(1);
        counts.SecondOutbox.ShouldBe(1);
    }

    [Fact]
    public async Task Relay_without_an_application_publisher_does_not_use_another_contexts_transport_alias()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await Scenario.EnqueueSecondOutboxEvent(ct);

        // Act
        Func<Task> publish = async () => _ = await Scenario.PublishSecondOutbox(ct);

        // Assert
        var exception = await publish.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldContain("application", StringComparison.OrdinalIgnoreCase);
        var state = await Scenario.GetSecondOutboxState(ct);
        state.PublishedAt.ShouldBeNull();
        state.Attempts.ShouldBe(0);
        state.LastAttemptAt.ShouldBeNull();
        state.ClaimedBy.ShouldBeNull();
        state.ClaimedUntil.ShouldBeNull();
        var firstTransportCount = await Scenario.CountFirstTransportMessages(ct);
        firstTransportCount.ShouldBe(0);
    }

    [Fact]
    public async Task Relay_without_an_application_publisher_keeps_its_same_context_transport_producer()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        var result = await Scenario.PublishFirstOutboxThroughItsProducer(ct);

        // Assert
        result.PublishedAt.ShouldNotBeNull();
        result.TransportCount.ShouldBe(1);
    }

    [Fact]
    public async Task Consumer_without_an_application_publisher_does_not_use_another_contexts_transport_alias()
    {
        // Arrange
        const string eventId = "cross-context-consumer";
        var ct = TestContext.Current.CancellationToken;
        await Scenario.SeedSecondTransportMessage(eventId, ct);

        // Act
        Func<Task> consume = async () => _ = await Scenario.ConsumeSecondTransport(ct);

        // Assert
        var exception = await consume.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldContain("application", StringComparison.OrdinalIgnoreCase);
        var state = await Scenario.GetSecondTransportState(eventId, ct);
        state.ProcessedAt.ShouldBeNull();
        state.Attempts.ShouldBe(0);
        state.LastAttemptAt.ShouldBeNull();
        state.ClaimedBy.ShouldBeNull();
        state.ClaimedUntil.ShouldBeNull();
        var firstTransportCount = await Scenario.CountFirstTransportMessages(ct);
        firstTransportCount.ShouldBe(0);
    }
}
