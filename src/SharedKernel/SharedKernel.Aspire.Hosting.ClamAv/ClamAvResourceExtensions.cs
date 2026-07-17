using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting;

/// <summary>
/// Adds private ClamAV daemon resources to an Aspire AppHost model.
/// </summary>
public static class ClamAvResourceExtensions
{
    private const string ImageTag = "1.5";
    private const string ImageDigest = "6f4a9e7d616ffc8d1070200fe35ac860735fdd522161a1043f94856e6ee13c28";
    private const string ResourceNameSuffixConfigurationKey = "DcpPublisher:ResourceNameSuffix";

    /// <summary>
    /// Adds a private ClamAV daemon with persistent virus definitions.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The resource name.</param>
    /// <returns>The configured ClamAV container resource.</returns>
    public static IResourceBuilder<ClamAvResource> AddClamAv(this IDistributedApplicationBuilder builder, [ResourceName] string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var resourceNameSuffix = builder.Configuration[ResourceNameSuffixConfigurationKey]?.Trim();
        var definitionsVolumeName = string.IsNullOrWhiteSpace(resourceNameSuffix)
            ? $"{name}-definitions"
            : $"{name}-{resourceNameSuffix}-definitions";
        var resource = new ClamAvResource(name);
        var resourceBuilder = builder.AddResource(resource)
            .WithImage("clamav/clamav", ImageTag)
            .WithImageSHA256(ImageDigest)
            .WithEndpoint(targetPort: 3310, name: ClamAvResource.TcpEndpointName, scheme: "tcp", isExternal: false)
            .WithVolume(definitionsVolumeName, "/var/lib/clamav");
        var healthCheckName = $"{name}-tcp-ping";
        builder.Services.AddHealthChecks().AddCheck(healthCheckName, new ClamAvPingHealthCheck(resourceBuilder.GetEndpoint(ClamAvResource.TcpEndpointName)));

        return resourceBuilder.WithHealthCheck(healthCheckName);
    }

    /// <summary>
    /// Enables or disables the FreshClam daemon inside the ClamAV container.
    /// </summary>
    /// <param name="resource">The ClamAV resource.</param>
    /// <param name="enabled">A value indicating whether FreshClam should run.</param>
    /// <returns>The configured ClamAV container resource.</returns>
    public static IResourceBuilder<ClamAvResource> WithFreshClam(this IResourceBuilder<ClamAvResource> resource, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(resource);

        return enabled ? resource : resource.WithEnvironment("CLAMAV_NO_FRESHCLAMD", "true");
    }

    /// <summary>
    /// Injects the private ClamAV host and port into a consuming application resource.
    /// </summary>
    /// <param name="destination">The application resource that scans media.</param>
    /// <param name="clamAv">The ClamAV resource.</param>
    /// <typeparam name="TDestination">The destination resource type.</typeparam>
    /// <returns>The configured destination resource.</returns>
    public static IResourceBuilder<TDestination> WithClamAvReference<TDestination>(
        this IResourceBuilder<TDestination> destination,
        IResourceBuilder<ClamAvResource> clamAv)
        where TDestination : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(clamAv);

        var endpoint = clamAv.GetEndpoint(ClamAvResource.TcpEndpointName);
        return destination
            .WithEnvironment("MalwareScanning__ClamAv__Host", endpoint.Property(EndpointProperty.Host))
            .WithEnvironment("MalwareScanning__ClamAv__Port", endpoint.Property(EndpointProperty.Port));
    }
}
