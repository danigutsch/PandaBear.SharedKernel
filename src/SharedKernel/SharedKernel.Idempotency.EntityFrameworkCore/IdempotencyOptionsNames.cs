using Microsoft.EntityFrameworkCore;

namespace PandaBear.Idempotency.EntityFrameworkCore;

/// <summary>Builds the named-options keys under which per-context idempotency options are registered.</summary>
internal static class IdempotencyOptionsNames
{
    /// <summary>Returns the named-options key for the storage options of <typeparamref name="TContext" />, so each context keeps its own settings.</summary>
    public static string Storage<TContext>()
        where TContext : DbContext => $"{typeof(TContext).AssemblyQualifiedName}:idempotency-storage";
}
