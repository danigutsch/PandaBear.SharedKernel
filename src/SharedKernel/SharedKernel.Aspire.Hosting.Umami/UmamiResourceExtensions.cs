using System.Text.RegularExpressions;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Aspire.Hosting;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding a self-hosted Umami web analytics resource to the application model.
/// </summary>
public static partial class UmamiResourceExtensions
{
    /// <summary>The header Umami reads the visitor's address from, the default of the Umami page-view target.</summary>
    public const string ClientIpHeader = "X-Forwarded-For";

    private const int HttpPort = 3000;
    private const string HeartbeatPath = "/api/heartbeat";
    private const string DatabaseName = "umami";
    private const string WebsiteResourceType = "UmamiWebsite";
    private const int MaximumDomainLength = 500;

    /// <summary>
    /// Adds an Umami container with its own PostgreSQL server and database to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    /// <remarks>
    /// <para>
    /// The PostgreSQL server <c>{name}-postgres</c> (the pinned image of <c>PostgresContainerImageTags</c>) holds the database
    /// <c>umami</c> as the resource <c>{name}-db</c>. Umami's <c>APP_SECRET</c> is the generated, persisted secret parameter
    /// <c>{name}-app-secret</c>. The container reads the visitor's address from <see cref="ClientIpHeader"/>
    /// (<c>CLIENT_IP_HEADER</c>), binds to every interface (<c>HOSTNAME=0.0.0.0</c>, which Podman would otherwise replace) and sends
    /// Umami no usage telemetry (<c>DISABLE_TELEMETRY=1</c>).
    /// </para>
    /// <para>
    /// This resource includes a built-in health check of <c>/api/heartbeat</c> and waits for its database. When it is referenced as a
    /// dependency using the <see cref="ResourceBuilderExtensions.WaitFor{T}(IResourceBuilder{T}, IResourceBuilder{IResource})"/>
    /// extension method then the dependent resource will wait until Umami answers. A site that sends page views references a website of
    /// it, added with <see cref="AddWebsite"/>, which is created in Umami when Umami is ready.
    /// </para>
    /// <para>
    /// Like every Aspire resource it lives as long as the AppHost run: without <c>WithDataVolume</c> or <c>WithDataBindMount</c> the
    /// analytics data is discarded with the PostgreSQL container. This version of the package uses the
    /// <inheritdoc cref="UmamiContainerImageTags.Image"/> container image from <inheritdoc cref="UmamiContainerImageTags.Registry"/>,
    /// pinned by digest to its <inheritdoc cref="UmamiContainerImageTags.Tag"/> tag.
    /// </para>
    /// </remarks>
    public static IResourceBuilder<UmamiResource> AddUmami(this IDistributedApplicationBuilder builder, [ResourceName] string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var database = builder.AddPostgres($"{name}-postgres")
            .WithImageTag(PostgresContainerImageTags.Tag)
            .WithImageSHA256(PostgresContainerImageTags.Digest)
            .AddDatabase($"{name}-db", DatabaseName);
        return AddUmami(builder, name, database, ownsDatabaseServer: true);
    }

    /// <summary>
    /// Adds an Umami container that keeps its data in an existing PostgreSQL database, such as one of a server that other resources
    /// share, to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="database">
    /// The database Umami keeps its data in, added with <c>AddDatabase</c> on a PostgreSQL server of the application. Umami creates its
    /// tables in it on its first start, so give it a database of its own.
    /// </param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="database"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    /// <remarks>
    /// <para>
    /// Umami gets the database as <c>DATABASE_URL</c> and waits until it is healthy; everything else is as in
    /// <see cref="AddUmami(IDistributedApplicationBuilder, string)"/>, which adds a server of its own instead. The server belongs to
    /// whoever added it, so <see cref="WithDataVolume"/> and <see cref="WithDataBindMount"/> refuse to change it: keep the analytics data
    /// with the server's own <c>WithDataVolume</c> or <c>WithDataBindMount</c>.
    /// </para>
    /// </remarks>
    public static IResourceBuilder<UmamiResource> AddUmami(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<PostgresDatabaseResource> database)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(database);

        return AddUmami(builder, name, database, ownsDatabaseServer: false);
    }

    /// <summary>
    /// Keeps the analytics data in a named volume for the data folder of Umami's PostgreSQL server.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and the PostgreSQL resource names.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only volume.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Umami keeps its data on a PostgreSQL server that <c>AddUmami</c> did not add.</exception>
    /// <remarks>The Umami container keeps no state of its own; everything it records is in PostgreSQL.</remarks>
    public static IResourceBuilder<UmamiResource> WithDataVolume(this IResourceBuilder<UmamiResource> builder, string? name = null, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);

        PostgresServer(builder).WithDataVolume(name, isReadOnly);
        return builder;
    }

    /// <summary>
    /// Keeps the analytics data in a host folder bound to the data folder of Umami's PostgreSQL server.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="source">The source directory on the host to mount into the PostgreSQL container.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only mount.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="source"/> is <see langword="null"/>, empty, or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Umami keeps its data on a PostgreSQL server that <c>AddUmami</c> did not add.</exception>
    public static IResourceBuilder<UmamiResource> WithDataBindMount(this IResourceBuilder<UmamiResource> builder, string source, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        PostgresServer(builder).WithDataBindMount(source, isReadOnly);
        return builder;
    }

    /// <summary>
    /// Configures the host port that the Umami dashboard is exposed on instead of using randomly assigned port.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="port">The port to bind on the host. If <see langword="null"/> is used random port will be assigned.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IResourceBuilder<UmamiResource> WithHostPort(this IResourceBuilder<UmamiResource> builder, int? port)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithEndpoint(UmamiResource.HttpEndpointName, endpoint => endpoint.Port = port);
    }

    /// <summary>
    /// Adds a website to Umami, shown under it, whose connection string <c>Endpoint={Umami's base address};WebsiteId={id}</c> a site
    /// receives with <c>WithReference(website, connectionName: "umami")</c>. In run mode, as soon as Umami is ready, the website is
    /// created in Umami with that id unless it exists, so the site's page views are recorded with nothing to set up by hand.
    /// </summary>
    /// <param name="builder">The Umami resource builder.</param>
    /// <param name="name">The name of the resource, which is also the website's name in Umami.</param>
    /// <param name="domain">
    /// The website's public domain, such as <c>example.com</c>, in the lower-case form Umami 3.4 accepts (<c>localhost</c> with an
    /// optional port is accepted too); Umami uses it to keep the site's own pages out of the referrers.
    /// </param>
    /// <returns>The website resource builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank, or <paramref name="domain"/> is not a domain Umami accepts.</exception>
    /// <remarks>
    /// <para>
    /// The website id is a name-based UUID (RFC 9562, version 5) of the Umami resource name, the website name and the domain, so with
    /// <c>WithDataVolume</c> the next run finds the same website without storing anything; <see cref="WithWebsiteId"/> sets it instead.
    /// Setting it up signs in as <c>admin</c> with Umami's default password <c>umami</c>, which a fresh database has, or with the password
    /// of <see cref="WithAdminPassword"/>, and runs in the background: the website resource is <c>Waiting</c> until Umami is ready,
    /// <c>Starting</c> while it is set up, then <c>Running</c>, or <c>FailedToStart</c> with a log line that names the request and
    /// Umami's status code. A failure stops no other resource, and no log or state shows the password or the token.
    /// </para>
    /// <para>
    /// A site needs no <c>WaitFor</c> on the website: page views sent before it exists are refused by Umami, and the site drops and counts
    /// them. In publish mode nothing is set up; a deployment creates the website in its Umami and gives the site the same connection string.
    /// </para>
    /// </remarks>
    public static IResourceBuilder<UmamiWebsiteResource> AddWebsite(this IResourceBuilder<UmamiResource> builder, [ResourceName] string name, string domain)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (string.IsNullOrEmpty(domain) || domain.Length > MaximumDomainLength || !UmamiDomain().IsMatch(domain))
        {
            throw new ArgumentException(
                "The domain must be a lower-case domain such as example.com, without scheme or path, or localhost with an optional port.",
                nameof(domain));
        }

        var applicationBuilder = builder.ApplicationBuilder;
        var website = applicationBuilder.AddResource(new UmamiWebsiteResource(name, domain, builder.Resource))
            .WithParentRelationship(builder)
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = WebsiteResourceType,
                State = KnownResourceStates.Waiting,
                Properties = [],
            });
        if (applicationBuilder.ExecutionContext.IsRunMode)
        {
            applicationBuilder.Services.AddHttpClient(UmamiWebsiteProvisioner.HttpClientName).RedactLoggedHeaders(static _ => true);
            applicationBuilder.Eventing.Subscribe<ResourceReadyEvent>(builder.Resource, (ready, ct) =>
            {
                var services = ready.Services;
                var provisioner = new UmamiWebsiteProvisioner(
                    services.GetRequiredService<ResourceNotificationService>(),
                    services.GetRequiredService<ResourceLoggerService>(),
                    services.GetRequiredService<IHttpClientFactory>());
                _ = Task.Run(() => provisioner.Provision(website.Resource, ct), CancellationToken.None);
                return Task.CompletedTask;
            });
        }

        return website;
    }

    /// <summary>Sets the website id instead of the name-based one, for a website that exists in Umami with another id.</summary>
    /// <param name="builder">The website resource builder.</param>
    /// <param name="websiteId">The website id: a UUID of version 1 to 8 with the variant of RFC 9562, which Umami 3.4 requires.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="websiteId"/> is not a UUID that Umami accepts.</exception>
    public static IResourceBuilder<UmamiWebsiteResource> WithWebsiteId(this IResourceBuilder<UmamiWebsiteResource> builder, Guid websiteId)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (!UmamiWebsiteIds.IsAccepted(websiteId))
        {
            throw new ArgumentException("The website id must be a UUID of version 1 to 8 with the variant of RFC 9562, as Umami requires.", nameof(websiteId));
        }

        builder.Resource.WebsiteId = websiteId;
        return builder;
    }

    /// <summary>
    /// Sets the password of Umami's <c>admin</c> user that setting up a website signs in with, for an Umami whose data is kept and whose
    /// password was changed. Without it, Umami's default password <c>umami</c> is used, the one a fresh database has.
    /// </summary>
    /// <param name="builder">The Umami resource builder.</param>
    /// <param name="password">The secret parameter that holds the password.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="password"/> is <see langword="null"/>.</exception>
    public static IResourceBuilder<UmamiResource> WithAdminPassword(this IResourceBuilder<UmamiResource> builder, IResourceBuilder<ParameterResource> password)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(password);

        builder.Resource.AdminPasswordParameter = password.Resource;
        return builder;
    }

    /// <summary>The <c>DOMAIN_REGEX</c> that Umami 3.4.1 checks a website domain with (<c>src/lib/constants.ts</c>), anchored at the very end.</summary>
    /// <returns>The expression.</returns>
    [GeneratedRegex(@"^(localhost(:[1-9][0-9]{0,4})?|((?=[a-z0-9_-]{1,63}\.)(xn--)?[a-z0-9_-]+(-[a-z0-9_-]+)*\.)+(xn--)?[a-z0-9_-]{2,63})\z", RegexOptions.CultureInvariant)]
    private static partial Regex UmamiDomain();

    private static IResourceBuilder<UmamiResource> AddUmami(
        IDistributedApplicationBuilder builder,
        string name,
        IResourceBuilder<PostgresDatabaseResource> database,
        bool ownsDatabaseServer)
    {
        var appSecret = builder.AddParameter(
            $"{name}-app-secret",
            new GenerateParameterDefault { MinLength = 64, Special = false },
            secret: true,
            persist: true);
        var resource = new UmamiResource(name, database.Resource, appSecret.Resource) { OwnsDatabaseServer = ownsDatabaseServer };

        return builder.AddResource(resource)
            .WithImage(UmamiContainerImageTags.Image, UmamiContainerImageTags.Tag)
            .WithImageRegistry(UmamiContainerImageTags.Registry)
            .WithImageSHA256(UmamiContainerImageTags.Digest)
            .WithHttpEndpoint(targetPort: HttpPort, name: UmamiResource.HttpEndpointName)
            .WithEnvironment(context =>
            {
                context.EnvironmentVariables["DATABASE_URL"] = resource.Database.UriExpression;
                context.EnvironmentVariables["APP_SECRET"] = resource.AppSecretParameter;
                context.EnvironmentVariables["HOSTNAME"] = "0.0.0.0";
                context.EnvironmentVariables["CLIENT_IP_HEADER"] = ClientIpHeader;
                context.EnvironmentVariables["DISABLE_TELEMETRY"] = "1";
            })
            .WithHttpHealthCheck(HeartbeatPath, endpointName: UmamiResource.HttpEndpointName)
            .WaitFor(database);
    }

    private static IResourceBuilder<PostgresServerResource> PostgresServer(IResourceBuilder<UmamiResource> builder)
    {
        var resource = builder.Resource;
        return resource.OwnsDatabaseServer
            ? builder.ApplicationBuilder.CreateResourceBuilder(resource.Database.Parent)
            : throw new InvalidOperationException(
                $"Umami '{resource.Name}' keeps its data in the database '{resource.Database.Name}' of the PostgreSQL server '{resource.Database.Parent.Name}', "
                + "which AddUmami did not add; keep the data with that server's own WithDataVolume or WithDataBindMount.");
    }
}
