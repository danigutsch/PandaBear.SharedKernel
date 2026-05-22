using Microsoft.Extensions.Hosting;

namespace SharedKernel.Observability.Tests;

public sealed class ObservabilityBuilderExtensionsTests
{
    [Fact]
    public void Configure_OpenTelemetry_Can_Be_Called_And_Returns_Builder()
    {
        var builder = new HostApplicationBuilder();
        var result = builder.ConfigureOpenTelemetry();
        Assert.Same(builder, result);
    }
}
