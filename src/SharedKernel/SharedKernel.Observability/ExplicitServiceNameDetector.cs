using OpenTelemetry.Resources;

namespace SharedKernel.Observability;

/// <summary>
/// An OpenTelemetry resource detector that ensures service identity is set explicitly from application config or host.
/// </summary>
public sealed class ExplicitServiceNameDetector : IResourceDetector
{
    private readonly string _serviceName;
    private readonly string? _serviceVersion;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExplicitServiceNameDetector"/> class.
    /// </summary>
    /// <param name="serviceName">The explicit service name to use for resource identity.</param>
    /// <param name="serviceVersion">The explicit service version to use for resource identity.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="serviceName"/> is null or whitespace.</exception>
    public ExplicitServiceNameDetector(string serviceName, string? serviceVersion = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        _serviceName = serviceName;
        _serviceVersion = string.IsNullOrWhiteSpace(serviceVersion) ? null : serviceVersion;
    }

    /// <inheritdoc/>
    public Resource Detect()
    {
        Dictionary<string, object> attributes = new()
        {
            ["service.name"] = _serviceName
        };

        if (_serviceVersion is not null)
        {
            attributes["service.version"] = _serviceVersion;
        }

        return new Resource(attributes);
    }
}
