using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SharedKernel.AuditTrail;
using SharedKernel.EntityFrameworkCore;
using SharedKernel.Idempotency;

namespace SharedKernel.DocumentWorkflow.EntityFrameworkCore;

/// <summary>Registers the optional document workflow EF storage.</summary>
public static class DocumentWorkflowServiceCollectionExtensions
{
    /// <summary>Adds the document store and its context model configuration.</summary>
    /// <typeparam name="TContext">The context that owns the workflow table.</typeparam>
    /// <param name="services">The application's services.</param>
    /// <returns>The services collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the collection is null.</exception>
    public static IServiceCollection AddDocumentWorkflowStorage<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<DocumentWorkflowStore<TContext>>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDbContextConfiguration<TContext>, DocumentWorkflowDbContextConfiguration<TContext>>());
        return services;
    }

    /// <summary>Adds the storage and the handler for a host's generated mediator adapter.</summary>
    /// <typeparam name="TContext">The context that owns document and idempotency state.</typeparam>
    /// <typeparam name="TSource">The host's document source model.</typeparam>
    /// <param name="services">The application's services.</param>
    /// <returns>The services collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the collection is null.</exception>
    /// <remarks>
    /// Register the context-keyed idempotency store, finalizer, and transactional audit sink before resolving the handler.
    /// The host supplies the mediator adapter and its Result transaction behavior.
    /// </remarks>
    public static IServiceCollection AddDocumentFinalizationCommand<TContext, TSource>(this IServiceCollection services)
        where TContext : DbContext
        where TSource : notnull
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddDocumentWorkflowStorage<TContext>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped(static provider => new FinalizeDocumentRevisionHandler<TContext, TSource>(
            provider.GetRequiredService<TContext>(),
            provider.GetRequiredService<DocumentWorkflowStore<TContext>>(),
            provider.GetRequiredService<DocumentFinalizer<TSource>>(),
            provider.GetRequiredKeyedService<IIdempotencyStore>(typeof(TContext)),
            provider.GetRequiredService<IAuditTrailSink<DocumentFinalizationAuditEntry>>(),
            provider.GetRequiredService<TimeProvider>()));
        return services;
    }
}
