using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.Observability.Tests;

/// <summary>Tests the host builder extension methods that configure observability.</summary>
public sealed class ObservabilityBuilderExtensionsTests
{
    [Fact]
    public void Configure_opentelemetry_can_be_called_and_returns_builder()
    {
        // Arrange
        var builder = new HostApplicationBuilder();

        // Act
        var result = builder.ConfigureOpenTelemetry();

        // Assert
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void Add_application_version_logging_registers_hosted_service()
    {
        // Arrange
        var builder = new HostApplicationBuilder();

        // Act
        var result = builder.AddApplicationVersionLogging();
        using var host = builder.Build();
        var hostedServices = host.Services.GetServices<IHostedService>();

        // Assert
        result.ShouldBeSameAs(builder);
        hostedServices.ShouldContain(service => string.Equals(
            service.GetType().Name,
            "ApplicationVersionLoggingService",
            StringComparison.Ordinal));
    }
}
