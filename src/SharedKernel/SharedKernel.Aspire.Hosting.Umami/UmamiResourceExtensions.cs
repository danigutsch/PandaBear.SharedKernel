using Aspire.Hosting.ApplicationModel;
using PandaBear.Aspire.Hosting;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding a self-hosted Umami web analytics resource to the application model.
/// </summary>
public static class UmamiResourceExtensions
{
    /// <summary>The header Umami reads the visitor's address from, the default of the server-side page-view forwarder.</summary>
    public const string ClientIpHeader = "X-Forwarded-For";

    private const int HttpPort = 3000;
    private const string HeartbeatPath = "/api/heartbeat";
    private const string DatabaseName = "umami";

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
    /// extension method then the dependent resource will wait until Umami answers. A site gets the base address of Umami as the
    /// connection string <c>ConnectionStrings__{name}</c> with <c>WithReference</c>.
    /// </para>
    /// <para>
    /// Without <c>WithDataVolume</c> or <c>WithDataBindMount</c> the analytics data lives and dies with the PostgreSQL container. On the
    /// first login with the default <c>admin</c> / <c>umami</c>, change the password, add the website and give its website id to the site.
    /// This version of the package uses the <inheritdoc cref="UmamiContainerImageTags.Image"/> container image from
    /// <inheritdoc cref="UmamiContainerImageTags.Registry"/>, pinned by digest to its <inheritdoc cref="UmamiContainerImageTags.Tag"/> tag.
    /// </para>
    /// </remarks>
    public static IResourceBuilder<UmamiResource> AddUmami(this IDistributedApplicationBuilder builder, [ResourceName] string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var appSecret = builder.AddParameter(
            $"{name}-app-secret",
            new GenerateParameterDefault { MinLength = 64, Special = false },
            secret: true,
            persist: true);
        var database = builder.AddPostgres($"{name}-postgres")
            .WithImageTag(PostgresContainerImageTags.Tag)
            .WithImageSHA256(PostgresContainerImageTags.Digest)
            .AddDatabase($"{name}-db", DatabaseName);
        var resource = new UmamiResource(name, database.Resource, appSecret.Resource);

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

    /// <summary>
    /// Keeps the analytics data in a named volume for the data folder of Umami's PostgreSQL server.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and the PostgreSQL resource names.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only volume.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
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

    private static IResourceBuilder<PostgresServerResource> PostgresServer(IResourceBuilder<UmamiResource> builder) =>
        builder.ApplicationBuilder.CreateResourceBuilder(builder.Resource.Database.Parent);
}
