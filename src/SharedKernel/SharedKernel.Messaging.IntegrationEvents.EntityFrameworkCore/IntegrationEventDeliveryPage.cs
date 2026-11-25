namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>A bounded page of pending delivery metadata.</summary>
/// <param name="Entries">The rows, in descending timestamp and identifier order.</param>
/// <param name="NextCursor">The position for the next page, or <see langword="null"/> at the end of the current results.</param>
public sealed record IntegrationEventDeliveryPage(
    IReadOnlyList<IntegrationEventDeliveryEntry> Entries, IntegrationEventDeliveryCursor? NextCursor);
