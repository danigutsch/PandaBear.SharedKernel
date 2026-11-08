using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.Mediator.Tests;

/// <summary>Builds the service providers of the scope initializer tests.</summary>
internal static class ScopeMarkerProviders
{
    /// <summary>Builds a provider with a scoped <see cref="ScopeMarker"/> and <see cref="MarkerSender"/>.</summary>
    /// <param name="log">The shared log.</param>
    /// <returns>The provider.</returns>
    public static ServiceProvider Create(ScopeLog log) =>
        new ServiceCollection()
            .AddSingleton(log)
            .AddScoped<ScopeMarker>()
            .AddScoped<ISender, MarkerSender>()
            .BuildServiceProvider();
}
