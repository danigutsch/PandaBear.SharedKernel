using Microsoft.EntityFrameworkCore;

namespace PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>Builds the named-options keys under which per-context integration event options are registered.</summary>
internal static class IntegrationEventOptionsNames
{
    /// <summary>Returns the named-options key for the storage options of <typeparamref name="TContext" />.</summary>
    public static string Storage<TContext>()
        where TContext : DbContext => $"{typeof(TContext).AssemblyQualifiedName}:integration-event-storage";

    /// <summary>Returns the named-options key for the outbox relay options of <typeparamref name="TContext" />.</summary>
    public static string Relay<TContext>()
        where TContext : DbContext => $"{typeof(TContext).AssemblyQualifiedName}:integration-event-outbox-relay";

    /// <summary>Returns the named-options key for the transport consumer options of <typeparamref name="TContext" />.</summary>
    public static string Consumer<TContext>()
        where TContext : DbContext => $"{typeof(TContext).AssemblyQualifiedName}:integration-event-transport-consumer";
}
