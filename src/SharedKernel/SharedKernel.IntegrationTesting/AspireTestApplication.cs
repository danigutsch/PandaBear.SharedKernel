using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace PandaBear.IntegrationTesting;

/// <summary>
/// Owns an Aspire distributed application lifetime for integration tests. It follows the log lines of every resource from the start, so a
/// readiness wait that runs out of time reports each pending resource's state and its last log lines. Every project resource it starts gets
/// <c>DOTNET_hostBuilder__reloadConfigOnChange=false</c>, so parallel test hosts do not exhaust the per-user file-watcher (inotify) limit.
/// </summary>
public sealed class AspireTestApplication : IAsyncDisposable
{
    private const string DcpResourceNameSuffixArgument = "--DcpPublisher:ResourceNameSuffix=";
    private const string ReloadConfigOnChangeVariable = "DOTNET_hostBuilder__reloadConfigOnChange";
    private static int _dcpResourceNameSuffixSequence;
    private static readonly TimeSpan DefaultResourceTeardownTimeout = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets the default timeout for resource startup waits.
    /// </summary>
    public static readonly TimeSpan DefaultResourceStartupTimeout = TimeSpan.FromSeconds(90);

    private IAsyncDisposable? _appBuilder;
    private DistributedApplication? _app;
    private ResourceLogTail? _logs;

    private AspireTestApplication(
        IAsyncDisposable? appBuilder,
        DistributedApplication app,
        ResourceLogTail logs)
    {
        _appBuilder = appBuilder;
        _app = app;
        _logs = logs;
    }

    /// <summary>
    /// Creates a resource-only Aspire testing builder with the standard concurrent-test defaults: a DCP resource-name suffix of its own
    /// (unless <paramref name="args"/> names one), so parallel test applications never share orchestrator resource names.
    /// </summary>
    /// <param name="args">Command-line configuration arguments for the builder.</param>
    /// <returns>The configured Aspire testing builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="args"/> is <see langword="null"/>.</exception>
    public static IDistributedApplicationTestingBuilder CreateBuilder(params string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return DistributedApplicationTestingBuilder.Create(CreateAppHostArguments(args));
    }

    /// <summary>
    /// Creates a resource-only Aspire testing builder with configuration values and the standard concurrent-test defaults of
    /// <see cref="CreateBuilder(string[])"/>.
    /// </summary>
    /// <param name="configuration">Configuration values; <paramref name="args"/> win over them.</param>
    /// <param name="args">Command-line configuration arguments for the builder.</param>
    /// <returns>The configured Aspire testing builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration"/> or <paramref name="args"/> is <see langword="null"/>.</exception>
    public static IDistributedApplicationTestingBuilder CreateBuilder(IReadOnlyDictionary<string, string?> configuration, params string[] args)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(args);
        return DistributedApplicationTestingBuilder.Create(
            CreateAppHostArguments(args),
            (_, settings) => AddConfiguration(settings, configuration));
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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="healthyResourceNames"/> or <paramref name="appHostArguments"/> is <see langword="null"/>.</exception>
    /// <exception cref="TimeoutException">A resource was not healthy in time; the message holds its state and last log lines.</exception>
    /// <exception cref="AggregateException">Thrown when starting the application fails and tearing down the partially started application also fails.</exception>
    public static async Task<AspireTestApplication> Start<TAppHost>(
        IEnumerable<string> healthyResourceNames,
        TimeSpan? resourceStartupTimeout,
        IReadOnlyCollection<string> appHostArguments,
        CancellationToken ct)
        where TAppHost : class
    {
        return await Start<TAppHost>(
            healthyResourceNames,
            resourceStartupTimeout,
            appHostArguments,
            new Dictionary<string, string?>(),
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts an Aspire application with AppHost configuration arguments and values, and waits for resources. The AppHost reads the values
    /// as configuration before its own code runs (connection strings of servers the tests own, a data folder); the arguments win over them.
    /// </summary>
    /// <typeparam name="TAppHost">The AppHost entry-point type.</typeparam>
    /// <param name="healthyResourceNames">Resource names that must become healthy before the method returns.</param>
    /// <param name="resourceStartupTimeout">The resource startup timeout.</param>
    /// <param name="appHostArguments">AppHost configuration arguments.</param>
    /// <param name="configuration">AppHost configuration values.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The started test application.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="healthyResourceNames"/>, <paramref name="appHostArguments"/> or <paramref name="configuration"/> is <see langword="null"/>.</exception>
    /// <exception cref="TimeoutException">A resource was not healthy in time; the message holds its state and last log lines.</exception>
    /// <exception cref="AggregateException">Thrown when starting the application fails and tearing down the partially started application also fails.</exception>
    public static async Task<AspireTestApplication> Start<TAppHost>(
        IEnumerable<string> healthyResourceNames,
        TimeSpan? resourceStartupTimeout,
        IReadOnlyCollection<string> appHostArguments,
        IReadOnlyDictionary<string, string?> configuration,
        CancellationToken ct)
        where TAppHost : class
    {
        ArgumentNullException.ThrowIfNull(healthyResourceNames);
        ArgumentNullException.ThrowIfNull(appHostArguments);
        ArgumentNullException.ThrowIfNull(configuration);

        var timeout = resourceStartupTimeout ?? DefaultResourceStartupTimeout;
        using var deadline = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        var appBuilder = await DistributedApplicationTestingBuilder
            .CreateAsync<TAppHost>(
                CreateAppHostArguments(appHostArguments),
                (_, settings) => AddConfiguration(settings, configuration),
                linked.Token)
            .ConfigureAwait(false);
        return await StartBuilt(appBuilder, healthyResourceNames, timeout, deadline, linked.Token, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts an Aspire application from a testing builder and waits for the requested resources to become healthy.
    /// </summary>
    /// <param name="builder">The configured testing application builder.</param>
    /// <param name="healthyResourceNames">Resource names that must become healthy before the method returns.</param>
    /// <param name="resourceStartupTimeout">The resource startup timeout.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The started test application.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="healthyResourceNames"/> is <see langword="null"/>.</exception>
    /// <exception cref="TimeoutException">A resource was not healthy in time; the message holds its state and last log lines.</exception>
    /// <exception cref="AggregateException">Thrown when starting the application fails and tearing down the partially started application also fails.</exception>
    public static async Task<AspireTestApplication> Start(
        IDistributedApplicationTestingBuilder builder,
        IEnumerable<string> healthyResourceNames,
        TimeSpan? resourceStartupTimeout,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(healthyResourceNames);

        var timeout = resourceStartupTimeout ?? DefaultResourceStartupTimeout;
        using var deadline = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        return await StartBuilt(builder, healthyResourceNames, timeout, deadline, linked.Token, ct).ConfigureAwait(false);
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
    /// <exception cref="InvalidOperationException">Thrown when the resource has no configured connection string.</exception>
    public async Task<string> GetConnectionString(string resourceName, CancellationToken ct)
    {
        var connectionString = await App.GetConnectionStringAsync(resourceName, ct).ConfigureAwait(false);
        return connectionString ?? throw new InvalidOperationException($"{resourceName} connection string is not configured.");
    }

    /// <summary>Gets the last lines a resource logged since the application started (at most 50).</summary>
    /// <param name="resourceName">The resource name.</param>
    /// <returns>The lines, oldest first; error-stream lines start with <c>err: </c>.</returns>
    /// <exception cref="ArgumentException"><paramref name="resourceName"/> is blank.</exception>
    /// <exception cref="InvalidOperationException">The application was disposed.</exception>
    public IReadOnlyList<string> GetResourceLogs(string resourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        _ = App;
        return _logs?.Lines(resourceName) ?? [];
    }

    /// <summary>Waits until resources report healthy.</summary>
    /// <param name="resourceNames">The resources.</param>
    /// <param name="timeout">How long to wait.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that completes when every resource is healthy.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="resourceNames"/> is <see langword="null"/>.</exception>
    /// <exception cref="TimeoutException">A resource was not healthy in time; the message holds its state and last log lines.</exception>
    public async Task WaitForHealthy(IEnumerable<string> resourceNames, TimeSpan timeout, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(resourceNames);
        using var deadline = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        await WaitForHealthy(App, _logs!, [.. resourceNames], timeout, deadline, linked.Token, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts a resource that was added with explicit start, and waits until it is healthy. Use it to start an expensive resource (a
    /// container, a server) only when the first test needs it, typically through <see cref="OnDemandResources{T}"/>.
    /// </summary>
    /// <param name="resourceName">The resource name.</param>
    /// <param name="timeout">How long the start and the health wait may take.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that completes when the resource is healthy.</returns>
    /// <exception cref="ArgumentException"><paramref name="resourceName"/> is blank.</exception>
    /// <exception cref="InvalidOperationException">The application has no such resource, or the orchestrator refused the start.</exception>
    /// <exception cref="TimeoutException">The resource was not healthy in time; the message holds its state and last log lines.</exception>
    public async Task StartResource(string resourceName, TimeSpan timeout, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        var app = App;
        var resource = app.Services.GetRequiredService<DistributedApplicationModel>().Resources
            .FirstOrDefault(candidate => string.Equals(candidate.Name, resourceName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"The application has no resource named '{resourceName}'.");
        using var deadline = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        var started = await app.Services.GetRequiredService<ResourceCommandService>()
            .ExecuteCommandAsync(resource, KnownResourceCommands.StartCommand, linked.Token).ConfigureAwait(false);
        if (!started.Success)
        {
            throw new InvalidOperationException($"Starting '{resourceName}' failed: {started.Message}");
        }

        await WaitForHealthy(app, _logs!, [resourceName], timeout, deadline, linked.Token, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        var app = _app;
        var appBuilder = _appBuilder;
        var logs = _logs;
        _app = null;
        _appBuilder = null;
        _logs = null;

        var teardownFailures = await Teardown(app, appBuilder, logs).ConfigureAwait(false);
        if (teardownFailures.Count > 0)
        {
            throw new AggregateException("Aspire test application teardown failed.", teardownFailures);
        }
    }

    private static async Task<AspireTestApplication> StartBuilt(
        IDistributedApplicationTestingBuilder builder,
        IEnumerable<string> healthyResourceNames,
        TimeSpan timeout,
        CancellationTokenSource deadline,
        CancellationToken startupCt,
        CancellationToken ct)
    {
        DistributedApplication? app = null;
        ResourceLogTail? logs = null;
        try
        {
            ConfigureTestResources(builder);
            app = await builder.BuildAsync(startupCt).ConfigureAwait(false);

            await app.StartAsync(startupCt).ConfigureAwait(false);

            // Follow every resource's log lines once the orchestrator named its instances; the log service replays what came before.
            logs = ResourceLogTail.Watch(
                app.Services.GetRequiredService<ResourceLoggerService>(),
                app.Services.GetRequiredService<DistributedApplicationModel>().Resources);
            await WaitForHealthy(app, logs, [.. healthyResourceNames], timeout, deadline, startupCt, ct).ConfigureAwait(false);
            return new AspireTestApplication(builder, app, logs);
        }
        catch (Exception startupFailure)
        {
            var teardownFailures = await Teardown(app, builder, logs).ConfigureAwait(false);
            if (teardownFailures.Count > 0)
            {
                throw CreateStartupAndTeardownFailure(startupFailure, teardownFailures);
            }

            throw;
        }
    }

    private static async Task WaitForHealthy(
        DistributedApplication app,
        ResourceLogTail logs,
        IReadOnlyList<string> resourceNames,
        TimeSpan timeout,
        CancellationTokenSource deadline,
        CancellationToken waitCt,
        CancellationToken ct)
    {
        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();
        try
        {
            await Task.WhenAll(resourceNames.Select(name => notifications.WaitForResourceHealthyAsync(name, waitCt))).ConfigureAwait(false);
        }
        catch (OperationCanceledException cancellation) when (deadline.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            var report = new StringBuilder().Append(CultureInfo.InvariantCulture, $"Resources were not healthy within {timeout}.");
            foreach (var name in resourceNames)
            {
                var state = notifications.TryGetCurrentState(name, out var current)
                    ? $"{current.Snapshot.State?.Text ?? "unknown"}, health {current.Snapshot.HealthStatus?.ToString() ?? "unknown"}"
                    : "no state";
                _ = report.AppendLine().Append(CultureInfo.InvariantCulture, $"- {name}: {state}; last log lines:");
                foreach (var line in logs.Lines(name))
                {
                    _ = report.AppendLine().Append("    ").Append(line);
                }
            }

            throw new TimeoutException(report.ToString(), cancellation);
        }
    }

    private static void ConfigureTestResources(IDistributedApplicationTestingBuilder builder)
    {
        // DCP allows up to two minutes for resource cleanup; the host must not cancel it at 30 seconds.
        builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = DefaultResourceTeardownTimeout);
        builder.Eventing.Subscribe<BeforeStartEvent>((beforeStart, _) =>
        {
            // Aspire allocates proxyless ports outside the OS ephemeral range. Mixing DCP-reserved
            // proxy target ports with runtime-assigned container ports lets Podman claim the same port.
            foreach (var container in beforeStart.Model.Resources.OfType<ContainerResource>())
            {
                builder.CreateResourceBuilder(container).WithEndpointProxySupport(false);
            }

            // Every .NET host watches its appsettings files for changes, one inotify instance per file and host; parallel test runs then
            // exhaust the per-user limit (IOException "The configured user limit (128) on the number of inotify instances has been
            // reached"). Tests never reload configuration, so the documented host setting turns the watchers off.
            foreach (var project in beforeStart.Model.Resources.OfType<ProjectResource>())
            {
                builder.CreateResourceBuilder(project).WithEnvironment(ReloadConfigOnChangeVariable, "false");
            }

            return Task.CompletedTask;
        });
    }

    private static async Task<List<Exception>> Teardown(
        DistributedApplication? app,
        IAsyncDisposable? appBuilder,
        ResourceLogTail? logs)
    {
        var teardownFailures = new List<Exception>();
        if (logs is not null)
        {
            await CaptureTeardownFailure(() => logs.DisposeAsync().AsTask(), teardownFailures).ConfigureAwait(false);
        }

        if (app is not null)
        {
            await CaptureTeardownFailure(
                () => RunWithResourceTeardownTimeout(app.StopAsync),
                teardownFailures).ConfigureAwait(false);
            await CaptureTeardownFailure(() => app.DisposeAsync().AsTask(), teardownFailures).ConfigureAwait(false);
        }

        if (appBuilder is not null)
        {
            await CaptureTeardownFailure(() => appBuilder.DisposeAsync().AsTask(), teardownFailures)
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
        Func<Task> operation,
        List<Exception> teardownFailures)
    {
        try
        {
            await operation().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            teardownFailures.Add(exception);
        }
    }

    private static async Task RunWithResourceTeardownTimeout(Func<CancellationToken, Task> operation)
    {
        using var timeoutCts = new CancellationTokenSource(DefaultResourceTeardownTimeout);
        await operation(timeoutCts.Token).ConfigureAwait(false);
    }

    /// <summary>Started distributed application; throws when it was already disposed or never initialized.</summary>
    private DistributedApplication App => _app ?? throw new InvalidOperationException("Aspire test application is not initialized.");

    private static string[] CreateAppHostArguments(IReadOnlyCollection<string> appHostArguments)
    {
        // A suffix the caller names (a stack and run id) is kept; it isolates as well as a generated one.
        if (appHostArguments.Any(static argument =>
                argument.StartsWith(DcpResourceNameSuffixArgument, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(argument[DcpResourceNameSuffixArgument.Length..])))
        {
            return [.. appHostArguments];
        }

        var suffix = string.Create(
            CultureInfo.InvariantCulture,
            $"test-{Environment.ProcessId}-{Interlocked.Increment(ref _dcpResourceNameSuffixSequence)}");
        return
        [
            .. appHostArguments,
            DcpResourceNameSuffixArgument + suffix
        ];
    }

    private static void AddConfiguration(HostApplicationBuilderSettings settings, IReadOnlyDictionary<string, string?> configuration)
    {
        settings.Configuration ??= new ConfigurationManager();
        settings.Configuration.AddInMemoryCollection(configuration);
    }
}
