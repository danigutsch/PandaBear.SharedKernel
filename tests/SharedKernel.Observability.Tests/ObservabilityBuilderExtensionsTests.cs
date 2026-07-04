using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Observability.Tests;

public sealed class ObservabilityBuilderExtensionsTests
{
    [Fact]
    public void Configure_opentelemetry_can_be_called_and_returns_builder()
    {
        var builder = new HostApplicationBuilder();
        var result = builder.ConfigureOpenTelemetry();
        Assert.Same(builder, result);
    }

    [Fact]
    public void Add_application_version_logging_registers_hosted_service()
    {
        var builder = new HostApplicationBuilder();
        var result = builder.AddApplicationVersionLogging();
        using var host = builder.Build();
        var hostedServices = host.Services.GetServices<IHostedService>();

        Assert.Same(builder, result);
        Assert.Contains(hostedServices, service => string.Equals(
            service.GetType().Name,
            "ApplicationVersionLoggingService",
            StringComparison.Ordinal));
    }
}
