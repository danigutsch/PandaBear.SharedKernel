using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding ClamAV resources to the application model.
/// </summary>
public static class ClamAvResourceExtensions
{
    private const string DefinitionsPath = "/var/lib/clamav";

    /// <summary>
    /// Adds a ClamAV container to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <para>
    /// This resource includes built-in health checks. When this resource is referenced as a dependency
    /// using the <see cref="ResourceBuilderExtensions.WaitFor{T}(IResourceBuilder{T}, IResourceBuilder{IResource})"/>
    /// extension method then the dependent resource will wait until the ClamAV daemon is able to service requests.
    /// </para>
    /// This version of the package uses the <inheritdoc cref="ClamAvContainerImageTags.Image"/> container image, pinned by digest to its <inheritdoc cref="ClamAvContainerImageTags.Tag"/> tag.
    /// </remarks>
    public static IResourceBuilder<ClamAvResource> AddClamAv(this IDistributedApplicationBuilder builder, [ResourceName] string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var resource = new ClamAvResource(name);
        var healthCheckName = $"{name}-tcp-ping";
        builder.Services.AddHealthChecks().AddCheck(healthCheckName, new ClamAvPingHealthCheck(resource.PrimaryEndpoint));

        return builder.AddResource(resource)
            .WithImage(ClamAvContainerImageTags.Image, ClamAvContainerImageTags.Tag)
            .WithImageRegistry(ClamAvContainerImageTags.Registry)
            .WithImageSHA256(ClamAvContainerImageTags.Digest)
            .WithEndpoint(targetPort: 3310, name: ClamAvResource.TcpEndpointName, scheme: "tcp", isExternal: false)
            .WithHealthCheck(healthCheckName);
    }

    /// <summary>
    /// Adds a named volume for the virus definitions to a ClamAV container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and resource names.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only volume.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<ClamAvResource> WithDataVolume(this IResourceBuilder<ClamAvResource> builder, string? name = null, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), DefinitionsPath, isReadOnly);
    }

    /// <summary>
    /// Adds a bind mount for the virus definitions to a ClamAV container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="source">The source directory on the host to mount into the container.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only mount.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<ClamAvResource> WithDataBindMount(this IResourceBuilder<ClamAvResource> builder, string source, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        return builder.WithBindMount(source, DefinitionsPath, isReadOnly);
    }

    /// <summary>
    /// Configures the host port that the ClamAV resource is exposed on instead of using randomly assigned port.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="port">The port to bind on the host. If <see langword="null"/> is used random port will be assigned.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<ClamAvResource> WithHostPort(this IResourceBuilder<ClamAvResource> builder, int? port)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithEndpoint(ClamAvResource.TcpEndpointName, endpoint => endpoint.Port = port);
    }

    /// <summary>
    /// Configures whether the FreshClam daemon runs in a ClamAV container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="enabled"><see langword="true"/> to run the FreshClam daemon, which updates the virus definitions; otherwise <see langword="false"/>.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// FreshClam runs unless it is disabled. Disable it when the virus definitions are supplied by the image, a mounted volume, or another update process.
    /// </remarks>
    public static IResourceBuilder<ClamAvResource> WithFreshClam(this IResourceBuilder<ClamAvResource> builder, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithEnvironment("CLAMAV_NO_FRESHCLAMD", enabled ? "false" : "true");
    }

    /// <summary>
    /// Injects the host and port of a ClamAV resource into a destination resource as environment variables.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination resource.</typeparam>
    /// <param name="destination">The resource where the environment variables are injected.</param>
    /// <param name="clamAv">The ClamAV resource to connect to.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// The variables are <c>MalwareScanning__ClamAv__Host</c> and <c>MalwareScanning__ClamAv__Port</c>, which the
    /// <c>PandaBear.SharedKernel.MalwareScanning.ClamAv</c> package reads.
    /// </remarks>
    public static IResourceBuilder<TDestination> WithClamAvReference<TDestination>(
        this IResourceBuilder<TDestination> destination,
        IResourceBuilder<ClamAvResource> clamAv)
        where TDestination : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(clamAv);

        return destination
            .WithEnvironment("MalwareScanning__ClamAv__Host", clamAv.Resource.Host)
            .WithEnvironment("MalwareScanning__ClamAv__Port", clamAv.Resource.Port);
    }
}
