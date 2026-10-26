namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Prometheus container.
/// </summary>
/// <param name="name">The name of the resource.</param>
public sealed class PrometheusResource([ResourceName] string name) : ContainerResource(name)
{
    internal const string HttpEndpointName = "http";

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the primary endpoint for the Prometheus HTTP API.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, HttpEndpointName);

    /// <summary>
    /// Gets the host endpoint reference for this resource.
    /// </summary>
    public EndpointReferenceExpression Host => PrimaryEndpoint.Property(EndpointProperty.Host);

    /// <summary>
    /// Gets the port endpoint reference for this resource.
    /// </summary>
    public EndpointReferenceExpression Port => PrimaryEndpoint.Property(EndpointProperty.Port);
}
