using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace SharedKernel.HttpClients.Tests;

internal static class HttpClientDefaultsTestServices
{
    public static bool CanCreateClient(string name)
    {
        var services = new ServiceCollection();
        services.AddHttpClientDefaults();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient(name);

        return client is not null;
    }

    public static (bool TracingRegistered, bool MetricsRegistered) GetTelemetryRegistrations()
    {
        var services = new ServiceCollection();
        services.AddHttpClientDefaults();

        using var provider = services.BuildServiceProvider();
        var tracingRegistered = provider.GetService<TracerProvider>() is not null;
        var metricsRegistered = provider.GetService<MeterProvider>() is not null;

        return (tracingRegistered, metricsRegistered);
    }
}
