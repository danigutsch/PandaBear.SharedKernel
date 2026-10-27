using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace PandaBear.ObjectStorage.S3;

/// <summary>
/// Registers the S3-compatible object store.
/// </summary>
public static class S3ObjectStoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IObjectStore" /> backed by an S3-compatible endpoint.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Populates <see cref="S3ObjectStoreOptions" />, typically by binding a configuration section.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddS3ObjectStore(this IServiceCollection services, Action<S3ObjectStoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<S3ObjectStoreOptions>()
            .Configure(configure)
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<S3ObjectStoreOptions>, S3ObjectStoreOptionsValidator>());
        services.AddSingleton<IAmazonS3>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<S3ObjectStoreOptions>>().Value;
            return new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), new AmazonS3Config
            {
                ServiceURL = options.Endpoint!.AbsoluteUri,
                ForcePathStyle = options.ForcePathStyle,
                AuthenticationRegion = options.AuthenticationRegion
            });
        });
        services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics.AddAWSInstrumentation())
            .WithTracing(tracing => tracing
                .AddAWSInstrumentation(options => options.SuppressDownstreamInstrumentation = true)
                .AddProcessor(new S3SensitiveAttributeRedactionProcessor()));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IObjectStore, S3ObjectStore>();

        return services;
    }
}
