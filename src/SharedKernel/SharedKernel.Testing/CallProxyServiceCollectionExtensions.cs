using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.Testing;

/// <summary>Replaces service registrations with <see cref="CallProxy"/> wrappers, so a test hooks the service the application resolves.</summary>
public static class CallProxyServiceCollectionExtensions
{
    /// <summary>
    /// Replaces every registration of <typeparamref name="TService"/> without a key with one that resolves a <see cref="CallProxy"/> over
    /// what the original registration creates, keeping its lifetime and its place in the collection.
    /// </summary>
    /// <remarks>
    /// The original registration moves under a private key, so the container still builds the real implementation with its dependencies,
    /// in the same scope, and disposes it as before; an instance registration stays undisposed. <paramref name="configure"/> runs once, here,
    /// and every proxy shares the hooks. Keyed registrations of <typeparamref name="TService"/> are left alone. Call it after the
    /// application's own registrations.
    /// </remarks>
    /// <typeparam name="TService">The service interface; it must be public.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Adds the hooks.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is null.</exception>
    /// <exception cref="ArgumentException">A hook or replacement is registered for a name that matches no method of the service.</exception>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="TService"/> has no registration without a key, or <see cref="CallHooks.Returns"/> is registered for a name whose
    /// methods all return no value or all have out or ref parameters.
    /// </exception>
    public static IServiceCollection WrapWithCallProxy<TService>(this IServiceCollection services, Action<CallHooks> configure)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var hooks = new CallHooks();
        configure(hooks);
        hooks.Validate(typeof(TService));

        var wrapped = 0;
        var count = services.Count;
        for (var index = 0; index < count; index++)
        {
            var original = services[index];
            if (original.ServiceType != typeof(TService) || original.IsKeyedService)
            {
                continue;
            }

            var key = new object();
            services.Add(WithKey(original, key));
            services[index] = new ServiceDescriptor(
                typeof(TService),
                provider => CallProxy.Wrap((TService)provider.GetRequiredKeyedService(typeof(TService), key), hooks),
                original.Lifetime);
            wrapped++;
        }

        return wrapped > 0
            ? services
            : throw new InvalidOperationException(
                $"CallProxy found no registration of {typeof(TService).Name} to wrap; register the service before wrapping it.");
    }

    private static ServiceDescriptor WithKey(ServiceDescriptor original, object key)
    {
        if (original.ImplementationInstance is { } instance)
        {
            return new ServiceDescriptor(original.ServiceType, key, instance);
        }

        if (original.ImplementationFactory is { } factory)
        {
            return new ServiceDescriptor(original.ServiceType, key, (provider, _) => factory(provider), original.Lifetime);
        }

        return new ServiceDescriptor(original.ServiceType, key, original.ImplementationType!, original.Lifetime);
    }
}
