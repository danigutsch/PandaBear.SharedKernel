using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.ServiceDefaults.Tests;

/// <summary>Lists the types a service registration names.</summary>
internal static class RegisteredTypes
{
    /// <summary>Gets the service type, its type arguments and the implementation type of a registration.</summary>
    /// <param name="descriptor">The registration.</param>
    /// <returns>The types.</returns>
    internal static IEnumerable<Type> Of(ServiceDescriptor descriptor)
    {
        yield return descriptor.ServiceType;
        foreach (var argument in descriptor.ServiceType.GenericTypeArguments)
        {
            yield return argument;
        }

        var implementation = descriptor.IsKeyedService
            ? descriptor.KeyedImplementationType ?? descriptor.KeyedImplementationInstance?.GetType()
            : descriptor.ImplementationType ?? descriptor.ImplementationInstance?.GetType();
        if (implementation is not null)
        {
            yield return implementation;
        }
    }
}
