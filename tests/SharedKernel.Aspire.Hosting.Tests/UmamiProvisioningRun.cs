using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Aspire.Hosting.Tests;

/// <summary>An application model, not started, with Umami's endpoint allocated, whose services report the website's state and log.</summary>
internal sealed class UmamiProvisioningRun : IAsyncDisposable
{
    private readonly DistributedApplication _app;
    private readonly UmamiWebsiteResource _website;
    private readonly UmamiWebsiteProvisioner _provisioner;

    private UmamiProvisioningRun(DistributedApplication app, UmamiWebsiteResource website, UmamiWebsiteProvisioner provisioner)
    {
        _app = app;
        _website = website;
        _provisioner = provisioner;
    }

    /// <summary>Allocates Umami's endpoint, builds the application and a provisioner that talks to <paramref name="umami"/>.</summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="website">The website to set up.</param>
    /// <param name="umami">The fake Umami.</param>
    /// <returns>The run.</returns>
    public static UmamiProvisioningRun Create(IDistributedApplicationBuilder builder, IResourceBuilder<UmamiWebsiteResource> website, FakeUmami umami)
    {
        AllocatedEndpoints.Allocate(website.Resource.Umami, UmamiResource.HttpEndpointName, 3300);
        var app = builder.Build();
        var provisioner = new UmamiWebsiteProvisioner(
            app.Services.GetRequiredService<ResourceNotificationService>(),
            app.Services.GetRequiredService<ResourceLoggerService>(),
            new FakeHttpClientFactory(umami));
        return new UmamiProvisioningRun(app, website.Resource, provisioner);
    }

    /// <summary>Sets up the website as the ready Umami would.</summary>
    /// <returns>A task that completes when the outcome is reported.</returns>
    public Task Provision() => _provisioner.Provision(_website, TestContext.Current.CancellationToken);

    /// <summary>Gets the website resource's current state.</summary>
    /// <returns>The state text, or <see langword="null"/> when none was published.</returns>
    public string? State() =>
        _app.Services.GetRequiredService<ResourceNotificationService>().TryGetCurrentState(_website.Name, out var resourceEvent)
            ? resourceEvent.Snapshot.State?.Text
            : null;

    /// <summary>Gets the lines of the website resource's log so far: the backlog that a new watch of the log receives first.</summary>
    /// <returns>The lines.</returns>
    public async Task<string[]> Log()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await foreach (var backlog in _app.Services.GetRequiredService<ResourceLoggerService>().WatchAsync(_website).WithCancellation(timeout.Token))
        {
            return [.. backlog.Select(line => line.Content)];
        }

        return [];
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _app.DisposeAsync();
}
