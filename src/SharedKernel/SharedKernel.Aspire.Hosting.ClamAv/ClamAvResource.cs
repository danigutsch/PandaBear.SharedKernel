namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a ClamAV container.
/// </summary>
/// <param name="name">The name of the resource.</param>
public sealed class ClamAvResource([ResourceName] string name) : ContainerResource(name)
{
    /// <summary>
    /// The name of the endpoint of the ClamAV daemon (<c>clamd</c>).
    /// </summary>
    public const string TcpEndpointName = "tcp";

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the primary endpoint for the ClamAV daemon.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, TcpEndpointName);

    /// <summary>
    /// Gets the host endpoint reference for this resource.
    /// </summary>
    public EndpointReferenceExpression Host => PrimaryEndpoint.Property(EndpointProperty.Host);

    /// <summary>
    /// Gets the port endpoint reference for this resource.
    /// </summary>
    public EndpointReferenceExpression Port => PrimaryEndpoint.Property(EndpointProperty.Port);
}
