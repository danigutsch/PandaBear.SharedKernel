using Microsoft.EntityFrameworkCore;
using SharedKernel.EntityFrameworkCore;

namespace SharedKernel.Domain.EntityFrameworkCore;

/// <summary>
/// Adds domain-event dispatch interception to a DbContext.
/// </summary>
/// <typeparam name="TContext">The DbContext type.</typeparam>
internal sealed class DomainEventDispatchDbContextConfiguration<TContext> : IDbContextConfiguration<TContext>
    where TContext : DbContext
{
    private readonly DispatchDomainEventsSaveChangesInterceptor interceptor = new();

    /// <inheritdoc />
    public void ConfigureOptions(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        optionsBuilder.AddInterceptors(interceptor);
    }
}
