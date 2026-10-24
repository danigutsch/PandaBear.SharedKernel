using PandaBear.Messaging;
using PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;
using PandaBear.Testing;

namespace PandaBear.EntityFrameworkCore.Tests;

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
            TimeProvider.System.GetUtcNow());

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
            TimeProvider.System.GetUtcNow());

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
            TimeProvider.System.GetUtcNow());
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
    public void MarkPoisoned_records_the_final_attempt_and_leaves_no_retry_scheduled()
    {
        // Arrange
        var message = new IntegrationEventTransportMessage(
            Guid.CreateVersion7(),
            "consumer",
            TransportEnvelopeFactory.Create("poison-unit"),
            new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero));
        var attemptedAt = new DateTimeOffset(2026, 10, 24, 12, 5, 0, TimeSpan.Zero);

        // Act
        message.MarkPoisoned(attemptedAt, "handler failed");

        // Assert
        message.ConsumeAttempts.ShouldBe(1);
        message.PoisonedAt.ShouldBe(attemptedAt);
        message.LastConsumeAttemptAt.ShouldBe(attemptedAt);
        message.NextConsumeAttemptAt.ShouldBeNull();
        message.LastConsumeError.ShouldBe("handler failed");
    }

    [Fact]
    public void ReleaseFromPoison_makes_the_message_claimable_again_with_fresh_attempts()
    {
        // Arrange
        var message = new IntegrationEventTransportMessage(
            Guid.CreateVersion7(),
            "consumer",
            TransportEnvelopeFactory.Create("poison-release"),
            new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero));
        message.MarkPoisoned(new DateTimeOffset(2026, 10, 24, 12, 5, 0, TimeSpan.Zero), "handler failed");
        var releasedAt = new DateTimeOffset(2026, 10, 24, 13, 0, 0, TimeSpan.Zero);

        // Act
        message.ReleaseFromPoison(releasedAt);

        // Assert
        message.PoisonedAt.ShouldBeNull();
        message.ConsumeAttempts.ShouldBe(0);
        message.NextConsumeAttemptAt.ShouldBe(releasedAt);
    }

    [Fact]
    public void MarkProcessed_clears_retry_and_claim_state()
    {
        // Arrange
        var message = new IntegrationEventTransportMessage(
            Guid.CreateVersion7(),
            "catalog",
            TransportEnvelopeFactory.Create("event-3"),
            TimeProvider.System.GetUtcNow());
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
