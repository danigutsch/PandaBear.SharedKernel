namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>The last position returned by a delivery inspection page. Keep the same filters for the next request.</summary>
/// <param name="RecordedAt">When the storage row was created.</param>
/// <param name="StorageId">The stable, unique row identifier used to order equal timestamps.</param>
public readonly record struct IntegrationEventDeliveryCursor(DateTimeOffset RecordedAt, Guid StorageId);
