namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Tempo container.
/// </summary>
/// <param name="name">The name of the resource.</param>
public sealed class TempoResource([ResourceName] string name) : ContainerResource(name)
{
    /// <summary>Name of the OTLP gRPC endpoint that receives traces.</summary>
    internal const string OtlpGrpcEndpointName = "otlp-grpc";

    /// <summary>Name of the HTTP endpoint that serves the readiness check.</summary>
    internal const string HttpEndpointName = "http";

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the primary endpoint for the Tempo OTLP gRPC receiver.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, OtlpGrpcEndpointName);

    /// <summary>
    /// Gets the host endpoint reference for this resource.
    /// </summary>
    public EndpointReferenceExpression Host => PrimaryEndpoint.Property(EndpointProperty.Host);

    /// <summary>
    /// Gets the port endpoint reference for this resource.
    /// </summary>
    public EndpointReferenceExpression Port => PrimaryEndpoint.Property(EndpointProperty.Port);
}
