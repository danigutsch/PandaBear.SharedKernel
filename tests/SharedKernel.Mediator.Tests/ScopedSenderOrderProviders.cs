using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.Mediator.Tests;

/// <summary>Builds providers for the <see cref="ScopedSender"/> registration order tests.</summary>
internal static class ScopedSenderOrderProviders
{
    /// <summary>Builds a provider with the marker services plus the given registrations.</summary>
    /// <param name="register">Adds the registrations under test.</param>
    /// <returns>The built provider.</returns>
    internal static ServiceProvider Build(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ScopeLog());
        services.AddScoped<ScopeMarker>();
        services.AddScoped<ISender, MarkerSender>();
        register(services);
        return services.BuildServiceProvider();
    }

    /// <summary>Sets the marker value on the operation scope.</summary>
    /// <param name="operation">The operation scope provider.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>A completed task.</returns>
    internal static ValueTask Mark(IServiceProvider operation, string value)
    {
        operation.GetRequiredService<ScopeMarker>().Value = value;
        return ValueTask.CompletedTask;
    }
}
