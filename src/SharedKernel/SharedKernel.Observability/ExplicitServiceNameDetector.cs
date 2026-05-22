using OpenTelemetry.Resources;

namespace SharedKernel.Observability;

/// <summary>
/// An OpenTelemetry resource detector that ensures service.name is set explicitly from application config or host.
/// </summary>
public sealed class ExplicitServiceNameDetector : IResourceDetector
{
    private readonly string _serviceName;
    /// <summary>
    /// Initializes a new instance of the <see cref="ExplicitServiceNameDetector"/> class.
    /// </summary>
    /// <param name="serviceName">The explicit service name to use for resource identity.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="serviceName"/> is null or whitespace.</exception>
    public ExplicitServiceNameDetector(string serviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        _serviceName = serviceName;
    }

    /// <inheritdoc/>
    public Resource Detect() => new Resource(new Dictionary<string, object> { ["service.name"] = _serviceName });
}
