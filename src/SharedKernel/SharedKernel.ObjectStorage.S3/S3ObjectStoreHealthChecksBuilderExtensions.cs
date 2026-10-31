using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace PandaBear.ObjectStorage.S3;

/// <summary>
/// Registers the S3 object-store health check.
/// </summary>
public static class S3ObjectStoreHealthChecksBuilderExtensions
{
    /// <summary>
    /// Adds a check that lists at most one key of the bucket configured through <see cref="S3ObjectStoreServiceCollectionExtensions.AddS3ObjectStore" />.
    /// It never creates the bucket, and failure descriptions never include the endpoint, bucket, keys, or provider messages.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">The registration name.</param>
    /// <param name="failureStatus">The status reported on failure, or null for <see cref="HealthStatus.Unhealthy" />.</param>
    /// <param name="tags">The optional registration tags.</param>
    /// <returns>The health checks builder.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is <see langword="null"/>, empty, or white-space.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IHealthChecksBuilder AddS3ObjectStoreHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "object-storage",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return builder.Add(new HealthCheckRegistration(
            name,
            serviceProvider => new S3ObjectStoreHealthCheck(
                serviceProvider.GetRequiredService<IAmazonS3>(),
                serviceProvider.GetRequiredService<IOptions<S3ObjectStoreOptions>>()),
            failureStatus,
            tags));
    }
}
