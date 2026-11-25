using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Testing.AspNetCore.Tests;

/// <summary>Tests that an application's real entry point is reachable over a real socket.</summary>
[Trait(TestTraitNames.ScopeName, "Integration")]
[Trait(TestTraitNames.CapabilityName, "KestrelTestHost")]
public sealed class KestrelTestHostTests
{
    [Fact]
    public async Task The_application_is_served_on_a_loopback_port_with_the_given_environment_and_configuration()
    {
        // Arrange
        await using var host = KestrelTestHost.Start<Program>(
            "Staging",
            new Dictionary<string, string?> { ["Setting"] = "configured" });
        using var client = new HttpClient { BaseAddress = host.BaseAddress };

        // Act
        var text = await client.GetStringAsync(new Uri("/ping", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        text.ShouldBe("Staging|configured|hello from the app");
        host.BaseAddress.Host.ShouldBe("127.0.0.1");
        host.BaseAddress.Port.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Service_overrides_are_applied_after_the_applications_registrations()
    {
        // Arrange
        await using var host = KestrelTestHost.Start<Program>(
            configureTestServices: services => services.AddSingleton(new Greeter("overridden")));
        using var client = new HttpClient { BaseAddress = host.BaseAddress };

        // Act
        var text = await client.GetStringAsync(new Uri("/ping", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        text.ShouldEndWith("|overridden", StringComparison.Ordinal);
        host.Services.GetRequiredService<Greeter>().Text.ShouldBe("overridden");
    }

    [Fact]
    public async Task Every_host_listens_on_its_own_port()
    {
        // Arrange
        await using var first = KestrelTestHost.Start<Program>();
        await using var second = KestrelTestHost.Start<Program>();

        // Assert
        first.BaseAddress.Port.ShouldNotBe(second.BaseAddress.Port);
    }

    [Fact]
    public async Task An_existing_factory_keeps_its_overrides_on_a_loopback_port()
    {
        // Arrange
        await using var root = WebApplicationTestHost.Create<Program>(
            environment: "Staging",
            configuration: new Dictionary<string, string?> { ["Setting"] = "configured" });
        await using var factory = root.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton(new Greeter("via factory"))));
        await using var host = KestrelTestHost.StartFromFactory(factory);
        using var client = new HttpClient { BaseAddress = host.BaseAddress };

        // Act
        var text = await client.GetStringAsync(new Uri("/ping", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        text.ShouldBe("Staging|configured|via factory");
        host.BaseAddress.Host.ShouldBe("127.0.0.1");
        host.BaseAddress.Port.ShouldBeGreaterThan(0);
    }
}
