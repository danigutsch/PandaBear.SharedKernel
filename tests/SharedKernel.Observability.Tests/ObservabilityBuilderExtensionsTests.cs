using Microsoft.Extensions.Hosting;

namespace SharedKernel.Observability.Tests;

public class ObservabilityBuilderExtensions
{
    [Fact]
    public void configure_open_telemetry_can_be_called_and_returns_builder()
    {
        var builder = new HostApplicationBuilder();
        var result = builder.ConfigureOpenTelemetry();
        Assert.Same(builder, result);
    }
}
