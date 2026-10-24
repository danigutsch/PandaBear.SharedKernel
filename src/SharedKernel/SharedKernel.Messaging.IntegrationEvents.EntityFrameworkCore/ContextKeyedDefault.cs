using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>
/// Resolves the unkeyed service for context-keyed registrations: the explicitly chosen context, or the only
/// registered context. Several contexts without a choice fail instead of silently binding the first.
/// </summary>
/// <typeparam name="TService">The context-keyed service type.</typeparam>
internal static class ContextKeyedDefault<TService>
    where TService : class
{
    public static void Register(IServiceCollection services, Type contextType, string chooseMethodName)
    {
        services.AddSingleton(new Registration(contextType));
        services.TryAddScoped(serviceProvider => Resolve(serviceProvider, chooseMethodName));
    }

    public static void Choose(IServiceCollection services, Type contextType) =>
        services.AddSingleton(new Choice(contextType));

    private static TService Resolve(IServiceProvider serviceProvider, string chooseMethodName)
    {
        var registered = serviceProvider.GetServices<Registration>().Select(item => item.ContextType).Distinct().ToArray();
        var chosen = serviceProvider.GetServices<Choice>().Select(item => item.ContextType).Distinct().ToArray();
        if (chosen.Length > 1)
        {
            throw new InvalidOperationException(
                $"Several default contexts were chosen for {typeof(TService).Name}: {Names(chosen)}. Call {chooseMethodName}<TContext>() once.");
        }

        var context = chosen.Length == 1
            ? chosen[0]
            : registered.Length == 1
                ? registered[0]
                : throw new InvalidOperationException(
                    $"{typeof(TService).Name} is registered for several contexts ({Names(registered)}) and none is the default. " +
                    $"Resolve it by its context key, or call {chooseMethodName}<TContext>().");
        if (!registered.Contains(context))
        {
            throw new InvalidOperationException(
                $"{context.Name} was chosen as the default {typeof(TService).Name} context but has no {typeof(TService).Name} registration.");
        }

        return serviceProvider.GetRequiredKeyedService<TService>(context);
    }

    private static string Names(IEnumerable<Type> contexts) => string.Join(", ", contexts.Select(context => context.Name));

    private sealed record Registration(Type ContextType);

    private sealed record Choice(Type ContextType);
}
