using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SharedKernel.Messaging.IntegrationEvents.CloudEvents")]
[assembly: InternalsVisibleTo("SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore")]

namespace PandaBear.Messaging.IntegrationEvents;

/// <summary>Envelope constants shared with the CloudEvents and EF Core integration event packages.</summary>
internal static class IntegrationEventEnvelopeConstants
{
    /// <summary>Envelope specification name.</summary>
    internal const string Spec = "cloudevents";
    /// <summary>Envelope specification version.</summary>
    internal const string SpecVersion = "1.0";
}
