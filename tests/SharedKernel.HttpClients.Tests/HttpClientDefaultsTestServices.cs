using System.Net;
using Microsoft.Extensions.Http.Resilience;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using PandaBear.Testing.Http;
using PandaBear.Testing;
using Polly;

namespace PandaBear.HttpClients.Tests;

/// <summary>Builds service providers with the default HTTP client registration and runs requests through them to observe retry, circuit breaker and telemetry behavior.</summary>
internal static class HttpClientDefaultsTestServices
{
    private const string ClientName = "shared-kernel-resilience-tests";
    private static readonly byte[] StreamingPayload = "payment-payload"u8.ToArray();

    /// <summary>Gets the byte length of the streamed "payment-payload" request body.</summary>
    public static int StreamingPayloadLength => StreamingPayload.Length;

    /// <summary>Registers the HTTP client defaults and reports whether a client with the given name can be created.</summary>
    /// <param name="name">The client name to create.</param>
    /// <returns><see langword="true" /> when the factory returned a client.</returns>
    public static bool CanCreateClient(string name)
    {
        var services = new ServiceCollection();
        services.AddHttpClientDefaults();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient(name);

        return client is not null;
    }

    /// <summary>Registers the HTTP client defaults and reports whether a tracer provider and a meter provider can be resolved.</summary>
    /// <returns>Whether tracing and metrics providers are registered.</returns>
    public static (bool TracingRegistered, bool MetricsRegistered) GetTelemetryRegistrations()
    {
        var services = new ServiceCollection();
        services.AddHttpClientDefaults();

        using var provider = services.BuildServiceProvider();
        var tracingRegistered = provider.GetService<TracerProvider>() is not null;
        var metricsRegistered = provider.GetService<MeterProvider>() is not null;

        return (tracingRegistered, metricsRegistered);
    }

    /// <summary>Sends one request to a handler that fails transiently after accepting it, through a client with the default resilience (optionally customized), and records the outcome.</summary>
    /// <param name="method">The HTTP method of the request.</param>
    /// <param name="includeStreamingContent">Whether the request carries tracked stream content.</param>
    /// <param name="configureResilience">An optional customization of the standard resilience options.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The final status code, the number of attempts that reached the handler, how often the content was serialized and how many source bytes were read.</returns>
    public static async Task<(
        HttpStatusCode StatusCode,
        int AttemptCount,
        int ContentSerializationCount,
        long SourceBytesRead)> SendTransientAfterCommit(
        HttpMethod method,
        bool includeStreamingContent,
        Action<HttpStandardResilienceOptions>? configureResilience,
        CancellationToken ct)
    {
        using var handler = new TransientAfterCommitHandler();
        var services = new ServiceCollection();
        services.AddServiceDiscovery();
        if (configureResilience is null)
        {
            services.AddHttpClientDefaults();
        }
        else
        {
            services.AddHttpClientDefaults(configureResilience);
        }
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

    /// <summary>Customizes the retry and circuit breaker predicates to ignore 429 responses on paths under <c>/visitor-quota/</c>, sends 102 requests to a quota client that returns 429 for its first 101, then one request each to an ordinary client and a transient client, and records the outcomes.</summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The quota statuses and attempts, the ordinary and transient statuses and attempts, and the distinct request paths seen by the retry predicate.</returns>
    public static async Task<(
        HttpStatusCode[] QuotaStatuses,
        int QuotaAttempts,
        HttpStatusCode OrdinaryStatus,
        int OrdinaryAttempts,
        HttpStatusCode TransientStatus,
        int TransientAttempts,
        string[] ObservedPaths)> SendPathScopedQuotaResponses(CancellationToken ct)
    {
        const string quotaClientName = "quota-client";
        const string ordinaryClientName = "ordinary-client";
        using var quotaHandler = new TransientAfterCommitHandler(HttpStatusCode.TooManyRequests, failuresBeforeSuccess: 101);
        using var ordinaryHandler = new TransientAfterCommitHandler(HttpStatusCode.TooManyRequests);
        using var transientHandler = new TransientAfterCommitHandler();
        List<string> observedPaths = [];
        var services = new ServiceCollection();
        services.AddServiceDiscovery();
        services.AddHttpClientDefaults(options =>
        {
            var shouldRetry = options.Retry.ShouldHandle;
            var shouldBreak = options.CircuitBreaker.ShouldHandle;
            options.Retry.ShouldHandle = arguments =>
            {
                var path = arguments.Context.GetRequestMessage()?.RequestUri?.AbsolutePath;
                if (path is not null)
                {
                    observedPaths.Add(path);
                }

                return arguments.Outcome.Result?.StatusCode == HttpStatusCode.TooManyRequests
                    && path?.StartsWith("/visitor-quota/", StringComparison.Ordinal) == true
                    ? ValueTask.FromResult(false)
                    : shouldRetry(arguments);
            };
            options.CircuitBreaker.ShouldHandle = arguments =>
                arguments.Outcome.Result?.StatusCode == HttpStatusCode.TooManyRequests
                    && arguments.Context.GetRequestMessage()?.RequestUri?.AbsolutePath
                        .StartsWith("/visitor-quota/", StringComparison.Ordinal) == true
                    ? ValueTask.FromResult(false)
                    : shouldBreak(arguments);
        });
        services.AddHttpClient(quotaClientName, client => client.BaseAddress = new Uri("https://quota.example.test"))
            .ConfigurePrimaryHttpMessageHandler(() => quotaHandler);
        services.AddHttpClient(ordinaryClientName, client => client.BaseAddress = new Uri("https://ordinary.example.test"))
            .ConfigurePrimaryHttpMessageHandler(() => ordinaryHandler);
        services.AddHttpClient("transient-client", client => client.BaseAddress = new Uri("https://quota.example.test"))
            .ConfigurePrimaryHttpMessageHandler(() => transientHandler);
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using var quotaClient = factory.CreateClient(quotaClientName);
        using var transientClient = factory.CreateClient("transient-client");
        using var ordinaryClient = factory.CreateClient(ordinaryClientName);
        List<HttpStatusCode> statuses = [];
        for (var request = 0; request < 102; request++)
        {
            using var response = await quotaClient.GetAsync(new Uri("/visitor-quota/content", UriKind.Relative), ct).ConfigureAwait(false);
            statuses.Add(response.StatusCode);
        }

        using var ordinaryResponse = await ordinaryClient.GetAsync(new Uri("/content", UriKind.Relative), ct).ConfigureAwait(false);
        using var transientResponse = await transientClient.GetAsync(new Uri("/visitor-quota/transient", UriKind.Relative), ct).ConfigureAwait(false);
        return (statuses.ToArray(), quotaHandler.AttemptCount, ordinaryResponse.StatusCode,
            ordinaryHandler.AttemptCount, transientResponse.StatusCode, transientHandler.AttemptCount,
            observedPaths.Distinct(StringComparer.Ordinal).ToArray());
    }

}
