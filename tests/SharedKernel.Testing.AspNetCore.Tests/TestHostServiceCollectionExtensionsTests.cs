using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace PandaBear.Testing.AspNetCore.Tests;

/// <summary>Tests the service overrides that remove an application's hosted services and health checks in a test host.</summary>
public sealed class TestHostServiceCollectionExtensionsTests
{
    [Fact]
    public void RemoveHostedServices_removes_every_hosted_service_registration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddHostedService<RecordingHostedService>();
        services.AddSingleton<IHostedService>(new RecordingHostedService());

        // Act
        services.RemoveHostedServices();

        // Assert
        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public async Task The_application_still_serves_requests_without_its_hosted_services()
    {
        // Arrange
        await using var host = KestrelTestHost.Start<Program>(configureTestServices: services =>
            services.AddHostedService<RecordingHostedService>().RemoveHostedServices());
        using var client = new HttpClient { BaseAddress = host.BaseAddress };

        // Act
        var text = await client.GetStringAsync(new Uri("/ping", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        text.ShouldEndWith("|hello from the app", StringComparison.Ordinal);
        RecordingHostedService.Started.ShouldBeFalse("a removed hosted service must not start");
    }

    [Fact]
    public void ClearHealthChecks_removes_the_checks_registered_before_and_after_it()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddHealthChecks().AddCheck("database", () => HealthCheckResult.Unhealthy());

        // Act
        services.ClearHealthChecks();
        services.AddHealthChecks().AddCheck("registered-later", () => HealthCheckResult.Unhealthy());
        using var provider = services.BuildServiceProvider();
        var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

        // Assert
        registrations.ShouldBeEmpty();
    }
}
