using Microsoft.EntityFrameworkCore;

namespace SharedKernel.Idempotency.EntityFrameworkCore;

internal static class IdempotencyOptionsNames
{
    public static string Storage<TContext>()
        where TContext : DbContext => $"{typeof(TContext).AssemblyQualifiedName}:idempotency-storage";
}
