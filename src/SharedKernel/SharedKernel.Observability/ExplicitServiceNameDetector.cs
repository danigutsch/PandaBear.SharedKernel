using OpenTelemetry.Resources;

namespace SharedKernel.Observability;

/// <summary>
/// An OpenTelemetry resource detector that ensures service.name is set explicitly from application config or host.
/// </summary>
public sealed class ExplicitServiceNameDetector(string serviceName) : IResourceDetector
{
    public ExplicitServiceNameDetector(string serviceName)
        : this(serviceName, validate: true) { }

    private ExplicitServiceNameDetector(string serviceName, bool validate)
        : this(serviceName)
    {
        if (validate && string.IsNullOrWhiteSpace(serviceName))
            throw new ArgumentException("Service name cannot be null or whitespace.", nameof(serviceName));
    }

{
    private readonly string _serviceName = serviceName;

    /// <inheritdoc/>
    public Resource Detect() => new Resource(new Dictionary<string, object> { ["service.name"] = _serviceName });
}
