using OpenTelemetry.Resources;

namespace SharedKernel.Observability;

/// <summary>
/// An OpenTelemetry resource detector that ensures service.name is set explicitly from application config or host.
/// </summary>
public sealed class ExplicitServiceNameDetector(string serviceName) : IResourceDetector
{
    private readonly string _serviceName = serviceName;

    /// <inheritdoc/>
    public Resource Detect() => new Resource(new Dictionary<string, object> { ["service.name"] = _serviceName });
}
