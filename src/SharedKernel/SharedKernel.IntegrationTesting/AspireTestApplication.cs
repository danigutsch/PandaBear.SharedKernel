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
    private static readonly TimeSpan DefaultResourceTeardownTimeout = TimeSpan.FromSeconds(30);

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
            await RunWithResourceStartupTimeout(async startupCt =>
            {
                appBuilder = await DistributedApplicationTestingBuilder
                    .CreateAsync<TAppHost>(CreateAppHostArguments(appHostArguments), startupCt)
                    .ConfigureAwait(false);
                var builtApp = await appBuilder.BuildAsync(startupCt).ConfigureAwait(false);
                app = builtApp;

                await builtApp.StartAsync(startupCt).ConfigureAwait(false);
                foreach (var resourceName in healthyResourceNames)
                {
                    await builtApp.ResourceNotifications.WaitForResourceHealthyAsync(resourceName, startupCt).ConfigureAwait(false);
                }
            }, resourceStartupTimeout, ct).ConfigureAwait(false);

            var startedApp = app ?? throw new InvalidOperationException("The Aspire application did not start.");
            return new AspireTestApplication(appBuilder, startedApp);
        }
        catch (Exception startupFailure)
        {
            var teardownFailures = await DisposeAfterFailedStart(app, appBuilder).ConfigureAwait(false);
            if (teardownFailures.Count > 0)
            {
                throw CreateStartupAndTeardownFailure(startupFailure, teardownFailures);
            }

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
            var builtApp = app;
            await RunWithResourceStartupTimeout(async startupCt =>
            {
                await builtApp.StartAsync(startupCt).ConfigureAwait(false);
                foreach (var resourceName in healthyResourceNames)
                {
                    await builtApp.ResourceNotifications.WaitForResourceHealthyAsync(resourceName, startupCt).ConfigureAwait(false);
                }
            }, resourceStartupTimeout, ct).ConfigureAwait(false);

            return new AspireTestApplication(null, app);
        }
        catch (Exception startupFailure)
        {
            var teardownFailures = await DisposeAfterFailedStart(app, null).ConfigureAwait(false);
            if (teardownFailures.Count > 0)
            {
                throw CreateStartupAndTeardownFailure(startupFailure, teardownFailures);
            }

            throw;
        }
    }

    /// <summary>
    /// Starts an Aspire application from a testing builder and waits for the requested resources to become healthy.
    /// </summary>
    /// <param name="builder">The configured testing application builder.</param>
    /// <param name="healthyResourceNames">Resource names that must become healthy before the method returns.</param>
    /// <param name="resourceStartupTimeout">The resource startup timeout.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The started test application.</returns>
    public static async Task<AspireTestApplication> Start(
        IDistributedApplicationTestingBuilder builder,
        IEnumerable<string> healthyResourceNames,
        TimeSpan? resourceStartupTimeout,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(healthyResourceNames);

        DistributedApplication? app = null;

        try
        {
            await RunWithResourceStartupTimeout(async startupCt =>
            {
                var builtApp = await builder.BuildAsync(startupCt).ConfigureAwait(false);
                app = builtApp;

                await builtApp.StartAsync(startupCt).ConfigureAwait(false);
                foreach (var resourceName in healthyResourceNames)
                {
                    await builtApp.ResourceNotifications.WaitForResourceHealthyAsync(resourceName, startupCt).ConfigureAwait(false);
                }
            }, resourceStartupTimeout, ct).ConfigureAwait(false);

            var startedApp = app ?? throw new InvalidOperationException("The Aspire application did not start.");
            return new AspireTestApplication(builder, startedApp);
        }
        catch (Exception startupFailure)
        {
            var teardownFailures = await DisposeAfterFailedStart(app, builder).ConfigureAwait(false);
            if (teardownFailures.Count > 0)
            {
                throw CreateStartupAndTeardownFailure(startupFailure, teardownFailures);
            }

            throw;
        }
    }

    private static async Task RunWithResourceStartupTimeout(
        Func<CancellationToken, Task> operation,
        TimeSpan? resourceStartupTimeout,
        CancellationToken ct)
    {
        using var timeoutCts = new CancellationTokenSource(resourceStartupTimeout ?? DefaultResourceStartupTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        await operation(linkedCts.Token).ConfigureAwait(false);
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

        var teardownFailures = new List<Exception>();
        if (app is not null)
        {
            await CaptureTeardownFailure(teardownCt => app.StopAsync(teardownCt), teardownFailures).ConfigureAwait(false);
            await CaptureTeardownFailure(teardownCt => app.DisposeAsync().AsTask(), teardownFailures).ConfigureAwait(false);
        }

        if (appBuilder is not null)
        {
            await CaptureTeardownFailure(teardownCt => appBuilder.DisposeAsync().AsTask(), teardownFailures)
                .ConfigureAwait(false);
        }

        if (teardownFailures.Count > 0)
        {
            throw new AggregateException("Aspire test application teardown failed.", teardownFailures);
        }
    }

    private static async Task<List<Exception>> DisposeAfterFailedStart(
        DistributedApplication? app,
        IAsyncDisposable? appBuilder)
    {
        var teardownFailures = new List<Exception>();
        if (app is not null)
        {
            await CaptureTeardownFailure(teardownCt => app.StopAsync(teardownCt), teardownFailures)
                .ConfigureAwait(false);
            await CaptureTeardownFailure(teardownCt => app.DisposeAsync().AsTask(), teardownFailures)
                .ConfigureAwait(false);
        }

        if (appBuilder is not null)
        {
            await CaptureTeardownFailure(teardownCt => appBuilder.DisposeAsync().AsTask(), teardownFailures)
                .ConfigureAwait(false);
        }

        return teardownFailures;
    }

    private static AggregateException CreateStartupAndTeardownFailure(
        Exception startupFailure,
        List<Exception> teardownFailures)
    {
        var failures = new List<Exception>(teardownFailures.Count + 1) { startupFailure };
        failures.AddRange(teardownFailures);
        return new AggregateException("Aspire test application startup and teardown failed.", failures);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Teardown must attempt every cleanup phase before reporting failures.")]
    private static async Task CaptureTeardownFailure(
        Func<CancellationToken, Task> operation,
        List<Exception> teardownFailures)
    {
        try
        {
            await RunWithResourceTeardownTimeout(operation).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            teardownFailures.Add(exception);
        }
    }

    private static async Task RunWithResourceTeardownTimeout(Func<CancellationToken, Task> operation)
    {
        using var timeoutCts = new CancellationTokenSource(DefaultResourceTeardownTimeout);
        await operation(timeoutCts.Token).WaitAsync(timeoutCts.Token).ConfigureAwait(false);
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
