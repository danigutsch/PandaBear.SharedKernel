namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>Optional exact matches and a lower time bound for pending delivery rows.</summary>
/// <param name="EventId">A stored event identifier, or <see langword="null"/> to include all identifiers.</param>
/// <param name="EventType">A stored event type, or <see langword="null"/> to include all types.</param>
/// <param name="RecordedSince">The inclusive lower bound on the storage timestamp, if supplied.</param>
public readonly record struct IntegrationEventDeliveryFilter(string? EventId, string? EventType, DateTimeOffset? RecordedSince)
{
    /// <summary>Gets the workflow correlation identifier to match, or null to include all workflows.</summary>
    public Guid? WorkflowCorrelationId { get; init; }
}
