namespace SharedKernel.Messaging.IntegrationEvents.CloudEvents;

/// <summary>
/// Provides transport metadata used when adapting typed integration events to CloudEvents.
/// </summary>
/// <param name="Source">The CloudEvents source URI.</param>
/// <param name="Subject">The optional CloudEvents subject.</param>
/// <param name="DataContentType">The optional data content type.</param>
/// <param name="DataSchema">The optional data schema URI.</param>
public sealed record CloudEventMetadata(
    Uri Source,
    string? Subject = null,
    string? DataContentType = null,
    Uri? DataSchema = null);
