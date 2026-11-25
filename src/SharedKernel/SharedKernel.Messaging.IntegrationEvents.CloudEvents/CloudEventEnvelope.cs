namespace SharedKernel.Messaging.IntegrationEvents.CloudEvents;

/// <summary>
/// Represents CloudEvents metadata for a typed integration event payload.
/// </summary>
/// <typeparam name="TIntegrationEvent">The typed integration event payload.</typeparam>
/// <param name="Id">The CloudEvents identifier.</param>
/// <param name="Source">The CloudEvents source URI.</param>
/// <param name="Type">The CloudEvents type.</param>
/// <param name="SpecVersion">The CloudEvents specification version.</param>
/// <param name="Time">The CloudEvents occurrence time.</param>
/// <param name="Subject">The optional CloudEvents subject.</param>
/// <param name="DataContentType">The optional data content type.</param>
/// <param name="DataSchema">The optional data schema URI.</param>
/// <param name="Version">The typed integration event contract version.</param>
/// <param name="Data">The typed integration event payload.</param>
public sealed record CloudEventEnvelope<TIntegrationEvent>(
    string Id,
    Uri Source,
    string Type,
    string SpecVersion,
    DateTimeOffset Time,
    string? Subject,
    string? DataContentType,
    Uri? DataSchema,
    int Version,
    TIntegrationEvent Data)
    where TIntegrationEvent : IIntegrationEvent;
