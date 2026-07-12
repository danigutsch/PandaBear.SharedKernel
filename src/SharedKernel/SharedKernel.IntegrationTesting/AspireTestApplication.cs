using Aspire.Hosting;
using Aspire.Hosting.Testing;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace SharedKernel.IntegrationTesting;

/// <summary>
/// Owns an Aspire distributed application lifetime for integration tests.
/// </summary>
public sealed class AspireTestApplication : IAsyncDisposable
{
    private static int _dcpResourceNameSuffixSequence;

    /// <summary>
    /// Gets the default timeout for resource startup waits.
    /// </summary>
    public static readonly TimeSpan DefaultResourceStartupTimeout = TimeSpan.FromSeconds(90);

    private IAsyncDisposable? _appBuilder;
    private DistributedApplication? _app;

    private AspireTestApplication(IAsyncDisposable? appBuilder, DistributedApplication app)
    {
        _appBuilder = appBuilder;
        _app = app;
    }

    /// <summary>
    /// Starts an Aspire application and waits for the requested resources to become healthy.
    /// </summary>
    /// <typeparam name="TAppHost">The AppHost entry-point type.</typeparam>
    /// <param name="healthyResourceNames">Resource names that must become healthy before the method returns.</param>
    /// <param name="resourceStartupTimeout">The resource startup timeout.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The started test application.</returns>
    public static async Task<AspireTestApplication> Start<TAppHost>(
        IEnumerable<string> healthyResourceNames,
        TimeSpan? resourceStartupTimeout,
        CancellationToken ct)
        where TAppHost : class
    {
        return await Start<TAppHost>(healthyResourceNames, resourceStartupTimeout, [], ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts an Aspire application with explicit AppHost configuration arguments and waits for resources.
    /// </summary>
    /// <typeparam name="TAppHost">The AppHost entry-point type.</typeparam>
    /// <param name="healthyResourceNames">Resource names that must become healthy before the method returns.</param>
    /// <param name="resourceStartupTimeout">The resource startup timeout.</param>
    /// <param name="appHostArguments">AppHost configuration arguments.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The started test application.</returns>
    public static async Task<AspireTestApplication> Start<TAppHost>(
        IEnumerable<string> healthyResourceNames,
        TimeSpan? resourceStartupTimeout,
        IReadOnlyCollection<string> appHostArguments,
        CancellationToken ct)
        where TAppHost : class
    {
        ArgumentNullException.ThrowIfNull(healthyResourceNames);
        ArgumentNullException.ThrowIfNull(appHostArguments);

        IDistributedApplicationTestingBuilder? appBuilder = null;
        DistributedApplication? app = null;

        try
        {
            appBuilder = await DistributedApplicationTestingBuilder
                .CreateAsync<TAppHost>(CreateAppHostArguments(appHostArguments), ct)
                .ConfigureAwait(false);
            app = await appBuilder.BuildAsync(ct).ConfigureAwait(false);
            await app.StartAsync(ct).ConfigureAwait(false);

            using var timeoutCts = new CancellationTokenSource(resourceStartupTimeout ?? DefaultResourceStartupTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            foreach (var resourceName in healthyResourceNames)
            {
                await app.ResourceNotifications.WaitForResourceHealthyAsync(resourceName, linkedCts.Token).ConfigureAwait(false);
            }

            return new AspireTestApplication(appBuilder, app);
        }
        catch
        {
            await DisposeAfterFailedStart(app, appBuilder).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Starts an Aspire application from an already configured builder.
    /// </summary>
    /// <param name="builder">The configured application builder.</param>
    /// <param name="healthyResourceNames">Resource names that must become healthy before the method returns.</param>
    /// <param name="resourceStartupTimeout">The resource startup timeout.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The started test application.</returns>
    public static async Task<AspireTestApplication> Start(
        IDistributedApplicationBuilder builder,
        IEnumerable<string> healthyResourceNames,
        TimeSpan? resourceStartupTimeout,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(healthyResourceNames);

        DistributedApplication? app = null;

        try
        {
            app = builder.Build();
            await app.StartAsync(ct).ConfigureAwait(false);

            using var timeoutCts = new CancellationTokenSource(resourceStartupTimeout ?? DefaultResourceStartupTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            foreach (var resourceName in healthyResourceNames)
            {
                await app.ResourceNotifications.WaitForResourceHealthyAsync(resourceName, linkedCts.Token).ConfigureAwait(false);
            }

            return new AspireTestApplication(null, app);
        }
        catch
        {
            await DisposeAfterFailedStart(app, null).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Creates an HTTP client for an Aspire resource.
    /// </summary>
    /// <param name="resourceName">The Aspire resource name.</param>
    /// <returns>An HTTP client for the resource.</returns>
    public HttpClient CreateHttpClient(string resourceName)
    {
        return App.CreateHttpClient(resourceName);
    }

    /// <summary>
    /// Gets the endpoint URI for an Aspire resource.
    /// </summary>
    /// <param name="resourceName">The Aspire resource name.</param>
    /// <param name="endpointName">The endpoint name.</param>
    /// <returns>The endpoint URI.</returns>
    public Uri GetEndpoint(string resourceName, string endpointName)
    {
        return App.GetEndpoint(resourceName, endpointName);
    }

    /// <summary>
    /// Gets a connection string for an Aspire resource.
    /// </summary>
    /// <param name="resourceName">The Aspire resource name.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The configured connection string.</returns>
    public async Task<string> GetConnectionString(string resourceName, CancellationToken ct)
    {
        var connectionString = await App.GetConnectionStringAsync(resourceName, ct).ConfigureAwait(false);
        return connectionString ?? throw new InvalidOperationException($"{resourceName} connection string is not configured.");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        var app = _app;
        var appBuilder = _appBuilder;
        _app = null;
        _appBuilder = null;

        try
        {
            if (app is not null)
            {
                await app.StopAsync().ConfigureAwait(false);
                await app.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            if (appBuilder is not null)
            {
                await appBuilder.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Cleanup after failed startup must not mask the original startup exception.")]
    private static async Task DisposeAfterFailedStart(
        DistributedApplication? app,
        IAsyncDisposable? appBuilder)
    {
        try
        {
            if (app is not null)
            {
                await app.StopAsync(CancellationToken.None).ConfigureAwait(false);
                await app.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch
        {
            // Preserve the original startup exception.
        }

        try
        {
            if (appBuilder is not null)
            {
                await appBuilder.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch
        {
            // Preserve the original startup exception.
        }
    }

    private DistributedApplication App => _app ?? throw new InvalidOperationException("Aspire test application is not initialized.");

    private static string[] CreateAppHostArguments(IReadOnlyCollection<string> appHostArguments)
    {
        var suffix = string.Create(
            CultureInfo.InvariantCulture,
            $"test-{Environment.ProcessId}-{Interlocked.Increment(ref _dcpResourceNameSuffixSequence)}");
        return [.. appHostArguments, $"--DcpPublisher:ResourceNameSuffix={suffix}"];
    }
}
