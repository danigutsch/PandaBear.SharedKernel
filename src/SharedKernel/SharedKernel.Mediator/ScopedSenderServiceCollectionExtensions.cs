using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PandaBear.Mediator;

/// <summary>
/// Provides registration helpers for <see cref="ScopedSender"/>.
/// </summary>
public static class ScopedSenderServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ScopedSender"/> with a scoped lifetime, so each instance holds the provider of the scope it is resolved in
    /// (for a Blazor component, the circuit scope). It is idempotent: if <see cref="ScopedSender"/> is already registered, with or without
    /// an initializer, it changes nothing. It does not register <see cref="ISender"/>; use
    /// <c>AddSharedKernelMediator</c> for that.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The configured service collection.</returns>
    /// <remarks>
    /// Every send runs in a new scope, in which circuit-scoped state is not present. To carry it over, use
    /// <see cref="AddScopedSender(IServiceCollection, Func{IServiceProvider, IServiceProvider, ValueTask})"/>. See <see cref="ScopedSender"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddScopedSender(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<ScopedSender>(static sp => new ScopedSender(sp.GetRequiredService<IServiceScopeFactory>(), sp));
        return services;
    }

    /// <summary>
    /// Registers <see cref="ScopedSender"/> with a scoped lifetime that runs <paramref name="scopeInitializer"/> on every new operation
    /// scope before sending, so an app can copy circuit state from the caller's scope into the new one. The initializer overload always
    /// wins: it replaces any earlier <see cref="ScopedSender"/> registration (including one from the parameterless overload), a later
    /// <see cref="AddScopedSender(IServiceCollection)"/> call does not undo it, and when called more than once the last initializer wins.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="scopeInitializer">
    /// The callback that runs once per send. It receives the provider of the scope the sender was resolved in first, and the new
    /// operation scope's provider second.
    /// </param>
    /// <returns>The configured service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="scopeInitializer"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddScopedSender(this IServiceCollection services, Func<IServiceProvider, IServiceProvider, ValueTask> scopeInitializer)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(scopeInitializer);

        services.Replace(ServiceDescriptor.Scoped<ScopedSender>(sp => new ScopedSender(sp.GetRequiredService<IServiceScopeFactory>(), sp, scopeInitializer)));
        return services;
    }
}
