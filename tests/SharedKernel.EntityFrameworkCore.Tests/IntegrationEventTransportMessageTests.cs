using SharedKernel.Messaging;
using SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;
using SharedKernel.Testing;

namespace SharedKernel.EntityFrameworkCore.Tests;

[Trait(SharedKernelTestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.IntegrationEventTransportCapability)]
public sealed class IntegrationEventTransportMessageTests
{
    [Fact]
    public void Constructor_rejects_null_envelope_before_reading_envelope_members()
    {
        // Arrange
        EventEnvelope? envelope = null;

        // Act
        Action create = () => _ = new IntegrationEventTransportMessage(
            Guid.CreateVersion7(),
            "catalog",
            envelope!,
            DateTimeOffset.UtcNow);

        // Assert
        var exception = create.ShouldThrow<ArgumentNullException>();
        exception.ParamName.ShouldBe("envelope");
    }

    [Fact]
    public void Constructor_truncates_consumer_name_to_storage_limit()
    {
        // Arrange
        var consumerName = new string('c', IntegrationEventTransportMessage.ConsumerNameMaxLength + 1);
        var envelope = TransportEnvelopeFactory.Create("event-1");

        // Act
        var message = new IntegrationEventTransportMessage(
            Guid.CreateVersion7(),
            consumerName,
            envelope,
            DateTimeOffset.UtcNow);

        // Assert
        message.ConsumerName.Length.ShouldBe(IntegrationEventTransportMessage.ConsumerNameMaxLength);
    }

    [Fact]
    public void MarkConsumeFailed_records_retry_state_and_truncates_error()
    {
        // Arrange
        var message = new IntegrationEventTransportMessage(
            Guid.CreateVersion7(),
            "catalog",
            TransportEnvelopeFactory.Create("event-2"),
            DateTimeOffset.UtcNow);
        var attemptedAt = new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);
        var nextAttemptAt = attemptedAt.AddSeconds(2);
        var error = new string('x', IntegrationEventTransportMessage.LastConsumeErrorMaxLength + 1);

        // Act
        message.MarkConsumeFailed(attemptedAt, nextAttemptAt, error);

        // Assert
        message.ConsumeAttempts.ShouldBe(1);
        message.LastConsumeAttemptAt.ShouldBe(attemptedAt);
        message.NextConsumeAttemptAt.ShouldBe(nextAttemptAt);
        message.LastConsumeError.ShouldNotBeNull().Length.ShouldBe(IntegrationEventTransportMessage.LastConsumeErrorMaxLength);
        message.ClaimedBy.ShouldBeNull();
        message.ClaimedUntil.ShouldBeNull();
    }

    [Fact]
    public void MarkProcessed_clears_retry_and_claim_state()
    {
        // Arrange
        var message = new IntegrationEventTransportMessage(
            Guid.CreateVersion7(),
            "catalog",
            TransportEnvelopeFactory.Create("event-3"),
            DateTimeOffset.UtcNow);
        var attemptedAt = new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);
        message.MarkConsumeFailed(attemptedAt, attemptedAt.AddSeconds(2), "failure");
        var processedAt = attemptedAt.AddSeconds(1);

        // Act
        message.MarkProcessed(processedAt);

        // Assert
        message.ProcessedAt.ShouldBe(processedAt);
        message.LastConsumeAttemptAt.ShouldBe(processedAt);
        message.NextConsumeAttemptAt.ShouldBeNull();
        message.LastConsumeError.ShouldBeNull();
        message.ClaimedBy.ShouldBeNull();
        message.ClaimedUntil.ShouldBeNull();
    }
}
