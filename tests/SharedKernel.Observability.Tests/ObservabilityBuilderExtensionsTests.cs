using Microsoft.Extensions.Hosting;

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
}
