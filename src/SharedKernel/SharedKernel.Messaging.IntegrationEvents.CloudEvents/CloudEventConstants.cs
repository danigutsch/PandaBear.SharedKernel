namespace SharedKernel.Messaging.IntegrationEvents.CloudEvents;

/// <summary>
/// Defines CloudEvents envelope constants used by messaging adapters.
/// </summary>
public static class CloudEventConstants
{
    /// <summary>
    /// The CloudEvents envelope specification name.
    /// </summary>
    public const string Spec = IntegrationEventEnvelopeConstants.Spec;

    /// <summary>
    /// The supported CloudEvents specification version.
    /// </summary>
    public const string SpecVersion = IntegrationEventEnvelopeConstants.SpecVersion;
}
