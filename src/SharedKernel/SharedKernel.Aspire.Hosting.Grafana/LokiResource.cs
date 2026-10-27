namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Loki container.
/// </summary>
/// <param name="name">The name of the resource.</param>
public sealed class LokiResource([ResourceName] string name) : ContainerResource(name)
{
    /// <summary>Name of the HTTP endpoint that serves the Loki API.</summary>
    internal const string HttpEndpointName = "http";

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the primary endpoint for the Loki HTTP API.
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
