using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.IntegrationTesting;

namespace SharedKernel.Aspire.Hosting.Umami.Testing;

/// <summary>
/// Knows how the Umami test website that test assemblies share is composed, borrowed from the process that owns it, and started. The
/// assembly fixtures and the shared test-services host call it, so they cannot drift apart.
/// </summary>
internal static class UmamiTestWebsite
{
    /// <summary>Names the environment variable that carries the connection string of a website in an Umami that another process owns.</summary>
    internal const string ConnectionStringVariable = "SHAREDKERNEL_TESTING_UMAMI_CONNECTION_STRING";

    /// <summary>
    /// The value that the owner of the shared Umami hands over in <see cref="ConnectionStringVariable"/> when Umami did not start. It is
    /// set, so a test assembly fails instead of starting an Umami of its own, and it is no connection string, so the failure says why.
    /// </summary>
    internal const string Unavailable = "Unavailable";

    /// <summary>The name of the Umami resource.</summary>
    internal const string UmamiResourceName = "umami";

    /// <summary>The name of the website resource, which is also the website's name in Umami.</summary>
    internal const string WebsiteResourceName = "site";

    /// <summary>The website's domain.</summary>
    internal const string Domain = "site.example";

    /// <summary>How long Umami and the website have to be ready, enough for an image pull on a cold machine.</summary>
    internal static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Adds Umami and the test website to <paramref name="builder"/>, and makes a wait for a resource stop as soon as it fails to start,
    /// so a website that cannot be set up fails at once instead of after <see cref="StartupTimeout"/>.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="server">
    /// A PostgreSQL server of the application on which Umami keeps its data in a database of its own, or <see langword="null"/> for a
    /// server of Umami's own.
    /// </param>
    /// <returns>The website resource builder.</returns>
    internal static IResourceBuilder<UmamiWebsiteResource> Add(IDistributedApplicationBuilder builder, IResourceBuilder<PostgresServerResource>? server = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.Configure<ResourceNotificationServiceOptions>(static options => options.DefaultWaitBehavior = WaitBehavior.StopOnResourceUnavailable);
        var umami = server is null
            ? builder.AddUmami(UmamiResourceName)
            : builder.AddUmami(UmamiResourceName, server.AddDatabase($"{UmamiResourceName}-db", "umami"));
        return umami.AddWebsite(WebsiteResourceName, Domain);
    }

    /// <summary>Reads the website of an Umami that another process owns.</summary>
    /// <param name="readEnvironment">Reads an environment variable; returns <see langword="null"/> when it is not set.</param>
    /// <returns>
    /// The website, or <see langword="null"/> when the variable is not set or blank, which means that the caller starts an Umami of its
    /// own on demand.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the variable holds <see cref="Unavailable"/>, or is not a connection string with an absolute <c>http</c> or
    /// <c>https</c> <c>Endpoint</c> and a <c>WebsiteId</c>. A set variable always borrows or fails; it never falls back to an Umami of the
    /// caller's own, which would hide a broken owner and start a second Umami. The message names the variable and never repeats its value.
    /// </exception>
    internal static UmamiTestWebsiteConnection? Borrowed(Func<string, string?> readEnvironment)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        var value = readEnvironment(ConnectionStringVariable)?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return value == Unavailable
            ? throw new InvalidOperationException(
                $"The process that owns the shared Umami set {ConnectionStringVariable} to {Unavailable}: Umami did not start there. Its standard error says why.")
            : Parse(value)
                ?? throw new InvalidOperationException(
                    $"The environment variable {ConnectionStringVariable} does not contain an Umami website connection string with an absolute http or https Endpoint and a WebsiteId.");
    }

    /// <summary>Composes Umami with a PostgreSQL server of its own on <paramref name="builder"/>, starts it, and waits until the website is set up.</summary>
    /// <param name="builder">
    /// A testing builder that was created from an assembly that uses the Aspire AppHost SDK, on the synchronous call path of this call,
    /// because Aspire finds its DCP metadata by walking the call stack.
    /// </param>
    /// <param name="ct">Cancels the start; <see cref="StartupTimeout"/> bounds it.</param>
    /// <returns>The started website. Disposing it stops the containers.</returns>
    internal static async Task<StartedUmamiTestWebsite> Start(IDistributedApplicationTestingBuilder builder, CancellationToken ct)
    {
        _ = Add(builder);
        var app = await AspireTestApplication.Start(builder, [UmamiResourceName, WebsiteResourceName], StartupTimeout, ct).ConfigureAwait(false);
        try
        {
            var connectionString = await app.GetConnectionString(WebsiteResourceName, ct).ConfigureAwait(false);
            var website = Parse(connectionString)
                ?? throw new InvalidOperationException($"The website resource {WebsiteResourceName} has no Umami website connection string.");
            return new StartedUmamiTestWebsite(website, app.DisposeAsync);
        }
        catch (Exception startFailure)
        {
            try
            {
                await app.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException("The Umami test website did not start, and stopping it failed too.", startFailure, cleanupFailure);
            }

            throw;
        }
    }

    /// <summary>
    /// Waits, as the owner of the shared Umami, until the website of <paramref name="application"/> is set up, and returns the value to hand
    /// over in <see cref="ConnectionStringVariable"/>.
    /// </summary>
    /// <param name="application">An application to which <see cref="Add"/> added Umami and that was started.</param>
    /// <param name="error">Receives the report when Umami does not start.</param>
    /// <param name="ct">Cancels the wait; <see cref="StartupTimeout"/> bounds it.</param>
    /// <returns>The website's connection string, or <see cref="Unavailable"/>.</returns>
    internal static Task<string> HandOver(AspireTestApplication application, TextWriter error, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(application);
        return HandOver(
            async token =>
            {
                await application.WaitForHealthy([UmamiResourceName, WebsiteResourceName], StartupTimeout, token).ConfigureAwait(false);
                return await application.GetConnectionString(WebsiteResourceName, token).ConfigureAwait(false);
            },
            error,
            ct);
    }

    /// <summary>
    /// Waits for the website with <paramref name="waitForWebsite"/> and returns its connection string. When the wait fails for any reason
    /// but <paramref name="ct"/>, it writes the failure to <paramref name="error"/> and returns <see cref="Unavailable"/>, so the owner still
    /// hands over its other services and only the tests that borrow Umami fail, each with a message that points at that report.
    /// </summary>
    /// <param name="waitForWebsite">Waits until the website is set up and returns its connection string.</param>
    /// <param name="error">Receives the report.</param>
    /// <param name="ct">Cancels the wait; a cancellation through it is not a failure of Umami and propagates.</param>
    /// <returns>The website's connection string, or <see cref="Unavailable"/>.</returns>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Whatever keeps Umami from starting is reported and handed over as Unavailable, so the services that did start still serve their tests.")]
    internal static async Task<string> HandOver(Func<CancellationToken, Task<string>> waitForWebsite, TextWriter error, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(waitForWebsite);
        ArgumentNullException.ThrowIfNull(error);
        try
        {
            var connectionString = await waitForWebsite(ct).ConfigureAwait(false);
            return Parse(connectionString) is null
                ? throw new InvalidOperationException($"The website resource {WebsiteResourceName} has no Umami website connection string.")
                : connectionString;
        }
        catch (Exception failure) when (!ct.IsCancellationRequested)
        {
            await error.WriteLineAsync(
                $"Umami did not start, so {ConnectionStringVariable} is {Unavailable}: the tests that borrow Umami fail, the other services are handed over.", ct)
                .ConfigureAwait(false);
            await error.WriteLineAsync(failure.ToString(), ct).ConfigureAwait(false);
            return Unavailable;
        }
    }

    private static UmamiTestWebsiteConnection? Parse(string connectionString)
    {
        var builder = new DbConnectionStringBuilder();
        try
        {
            builder.ConnectionString = connectionString;
        }
        catch (ArgumentException)
        {
            return null;
        }

        return builder.TryGetValue("Endpoint", out var endpoint)
            && Uri.TryCreate(endpoint as string, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && builder.TryGetValue("WebsiteId", out var websiteId)
            && Guid.TryParse(websiteId as string, out var id)
                ? new UmamiTestWebsiteConnection(connectionString, uri, id)
                : null;
    }
}
