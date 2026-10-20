using System.Net;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace SharedKernel.HttpClients.Tests;

internal static class HttpClientDefaultsTestServices
{
    private const string ClientName = "shared-kernel-resilience-tests";
    private static readonly byte[] StreamingPayload = "payment-payload"u8.ToArray();

    public static int StreamingPayloadLength => StreamingPayload.Length;

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

    public static async Task<(
        HttpStatusCode StatusCode,
        int AttemptCount,
        int ContentSerializationCount,
        long SourceBytesRead)> SendTransientAfterCommit(
        HttpMethod method,
        bool includeStreamingContent,
        CancellationToken ct)
    {
        using var handler = new TransientAfterCommitHandler();
        var services = new ServiceCollection();
        services.AddServiceDiscovery();
        services.AddHttpClientDefaults();
        services.AddHttpClient(ClientName, client => client.BaseAddress = new Uri("https://admin.example.test"))
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient(ClientName);
        using var request = new HttpRequestMessage(method, "/api/v1/bookings/1/payments");
        using var source = includeStreamingContent ? new TrackingReadStream(StreamingPayload) : null;
        TrackingStreamContent? content = null;
        if (source is not null)
        {
            content = new TrackingStreamContent(source);
            request.Content = content;
        }

        using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
        return (
            response.StatusCode,
            handler.AttemptCount,
            content?.SerializationCount ?? 0,
            source?.BytesRead ?? 0);
    }
}
