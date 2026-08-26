using Microsoft.EntityFrameworkCore;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

internal static class IntegrationEventOptionsNames
{
    public static string Storage<TContext>()
        where TContext : DbContext => $"{typeof(TContext).AssemblyQualifiedName}:integration-event-storage";

    public static string Relay<TContext>()
        where TContext : DbContext => $"{typeof(TContext).AssemblyQualifiedName}:integration-event-outbox-relay";

    public static string Consumer<TContext>()
        where TContext : DbContext => $"{typeof(TContext).AssemblyQualifiedName}:integration-event-transport-consumer";
}
