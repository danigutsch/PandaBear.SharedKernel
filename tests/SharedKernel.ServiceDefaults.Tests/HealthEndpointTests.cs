using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace SharedKernel.ServiceDefaults.Tests;

/// <summary>Tests the health endpoints that MapHealthEndpoints maps.</summary>
public sealed class HealthEndpointTests
{
    [Fact]
    public async Task Health_and_alive_are_mapped_outside_development_by_default_as_uncached_plain_text()
    {
        // Arrange
        await using var app = await DefaultsApplication.Start("Production");
        using var client = app.GetTestClient();

        // Act
        using var health = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);
        using var alive = await client.GetAsync(new Uri("/alive", UriKind.Relative), TestContext.Current.CancellationToken);
        var healthText = await health.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var aliveText = await alive.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        health.StatusCode.ShouldBe(HttpStatusCode.OK);
        healthText.ShouldBe("Healthy");
        health.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("text/plain");
        health.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue("a cache must never answer a probe");
        alive.StatusCode.ShouldBe(HttpStatusCode.OK);
        aliveText.ShouldBe("Healthy");
    }

    [Fact]
    public async Task Development_only_exposure_hides_the_endpoints_outside_development()
    {
        // Arrange
        await using var app = await DefaultsApplication.Start("Production", options => options.Exposure = HealthEndpointExposure.DevelopmentOnly);
        using var client = app.GetTestClient();

        // Act
        using var health = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);
        using var alive = await client.GetAsync(new Uri("/alive", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        health.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        alive.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Development_only_exposure_maps_the_endpoints_in_development()
    {
        // Arrange
        await using var app = await DefaultsApplication.Start("Development", options => options.Exposure = HealthEndpointExposure.DevelopmentOnly);
        using var client = app.GetTestClient();

        // Act
        using var health = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        health.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Alive_runs_only_live_checks_while_health_runs_every_check()
    {
        // Arrange
        await using var app = await DefaultsApplication.Start(
            "Production",
            arrange: builder => builder.Services.AddHealthChecks().AddCheck("database", () => HealthCheckResult.Unhealthy()));
        using var client = app.GetTestClient();

        // Act
        using var health = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);
        using var alive = await client.GetAsync(new Uri("/alive", UriKind.Relative), TestContext.Current.CancellationToken);
        var healthText = await health.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        health.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        healthText.ShouldBe("Unhealthy", "only the status, never the failing check");
        alive.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_paths_can_be_changed()
    {
        // Arrange
        await using var app = await DefaultsApplication.Start("Production", options =>
        {
            options.HealthPath = "/ready";
            options.AlivePath = "/livez";
        });
        using var client = app.GetTestClient();

        // Act
        using var ready = await client.GetAsync(new Uri("/ready", UriKind.Relative), TestContext.Current.CancellationToken);
        using var live = await client.GetAsync(new Uri("/livez", UriKind.Relative), TestContext.Current.CancellationToken);
        using var health = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        ready.StatusCode.ShouldBe(HttpStatusCode.OK);
        live.StatusCode.ShouldBe(HttpStatusCode.OK);
        health.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_endpoints_answer_anonymous_probes_when_the_host_requires_authentication_everywhere()
    {
        // Arrange
        await using var app = await DefaultsApplication.Start("Production", arrange: builder => builder.Services.AddAuthorization(options =>
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()));
        using var client = app.GetTestClient();

        // Act
        using var health = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);
        using var alive = await client.GetAsync(new Uri("/alive", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        health.StatusCode.ShouldBe(HttpStatusCode.OK);
        alive.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_liveness_self_check_is_registered_with_the_live_tag()
    {
        // Arrange
        await using var app = DefaultsApplication.Build("Production");

        // Act
        var registrations = app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

        // Assert
        registrations.ShouldContain(registration => registration.Name == "self" && registration.Tags.Contains(ServiceDefaultsExtensions.LiveTag));
    }

    [Fact]
    public async Task Mapping_without_adding_the_service_defaults_is_refused()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();

        // Act
        Action map = () => app.MapHealthEndpoints();

        // Assert
        map.ShouldThrow<InvalidOperationException>().Message.ShouldContain(nameof(ServiceDefaultsExtensions.AddApplicationDefaults), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("health", "/alive")]
    [InlineData("/health", " ")]
    [InlineData("/health", "/a\u00A0b")]
    [InlineData("/a\u2003b", "/alive")]
    [InlineData("/same", "/same")]
    public void Paths_that_do_not_start_with_a_slash_or_that_collide_are_refused(string healthPath, string alivePath)
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();

        // Act
        Action add = () => builder.AddApplicationDefaults(options =>
        {
            options.HealthPath = healthPath;
            options.AlivePath = alivePath;
        });

        // Assert
        add.ShouldThrow<ArgumentException>().ParamName.ShouldBe("configure");
    }
}
