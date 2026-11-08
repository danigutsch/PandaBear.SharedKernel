using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.Aspire.Hosting.Tests;

/// <summary>Publishes the <see cref="BeforeStartEvent"/> of an application builder without starting the application.</summary>
internal static class BeforeStartPublisher
{
    /// <summary>Publishes the event with the builder's current resources as the model.</summary>
    /// <param name="builder">The application builder whose subscribers receive the event.</param>
    /// <returns>A task that completes when all subscribers have run.</returns>
    public static async Task Publish(IDistributedApplicationBuilder builder)
    {
        await using var provider = builder.Services.BuildServiceProvider();
        var model = new DistributedApplicationModel(builder.Resources);
        await builder.Eventing.PublishAsync(new BeforeStartEvent(provider, model), TestContext.Current.CancellationToken);
    }
}
