using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SharedKernel.Messaging.IntegrationEvents.CloudEvents")]
[assembly: InternalsVisibleTo("SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore")]

namespace SharedKernel.Messaging.IntegrationEvents;

internal static class IntegrationEventEnvelopeConstants
{
    internal const string Spec = "cloudevents";
    internal const string SpecVersion = "1.0";
}
