using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;

namespace PandaBear.Aspire.Hosting.Tests;

/// <summary>
/// Simulates one AppHost run without orchestration: <see cref="Start"/> publishes the <see cref="BeforeStartEvent"/> with the builder's
/// services, and <see cref="Stop"/> raises the host's <see cref="IHostApplicationLifetime.ApplicationStopped"/>.
/// </summary>
internal sealed class AppHostRun : IAsyncDisposable
{
    private readonly ServiceProvider provider;

    private AppHostRun(ServiceProvider provider) => this.provider = provider;

    /// <summary>Builds the service provider and publishes the before-start event.</summary>
    /// <param name="builder">The application builder whose subscribers receive the event.</param>
    /// <returns>The started run.</returns>
    public static async Task<AppHostRun> Start(IDistributedApplicationBuilder builder)
    {
        var provider = builder.Services.BuildServiceProvider();
        var model = new DistributedApplicationModel(builder.Resources);
        await builder.Eventing.PublishAsync(new BeforeStartEvent(provider, model), TestContext.Current.CancellationToken);
        return new AppHostRun(provider);
    }

    /// <summary>Signals that the host has stopped, which runs the <see cref="IHostApplicationLifetime.ApplicationStopped"/> callbacks.</summary>
    public void Stop() => ((ApplicationLifetime)provider.GetRequiredService<IHostApplicationLifetime>()).NotifyStopped();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => provider.DisposeAsync();
}
