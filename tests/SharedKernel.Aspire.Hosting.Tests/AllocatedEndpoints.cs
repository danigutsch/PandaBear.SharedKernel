using Aspire.Hosting.ApplicationModel;

namespace PandaBear.Aspire.Hosting.Tests;

/// <summary>Gives endpoints the address that the orchestrator would allocate when the application starts.</summary>
internal static class AllocatedEndpoints
{
    /// <summary>Allocates <c>localhost:{port}</c> to the named endpoint.</summary>
    /// <param name="resource">The resource.</param>
    /// <param name="endpointName">The endpoint name.</param>
    /// <param name="port">The port.</param>
    internal static void Allocate(IResourceWithEndpoints resource, string endpointName, int port)
    {
        var endpoint = resource.Annotations.OfType<EndpointAnnotation>().Single(annotation => annotation.Name == endpointName);
        endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", port);
    }

    /// <summary>Allocates <c>{host}:{port}</c> to the named endpoint in the default container network, the address another container reaches it at.</summary>
    /// <param name="resource">The resource.</param>
    /// <param name="endpointName">The endpoint name.</param>
    /// <param name="host">The host name in the container network.</param>
    /// <param name="port">The port.</param>
    internal static void AllocateInContainerNetwork(IResourceWithEndpoints resource, string endpointName, string host, int port)
    {
        var endpoint = resource.Annotations.OfType<EndpointAnnotation>().Single(annotation => annotation.Name == endpointName);
        endpoint.AllAllocatedEndpoints.AddOrUpdateAllocatedEndpoint(
            KnownNetworkIdentifiers.DefaultAspireContainerNetwork,
            new AllocatedEndpoint(endpoint, host, port, EndpointBindingMode.SingleAddress, targetPortExpression: null, KnownNetworkIdentifiers.DefaultAspireContainerNetwork));
    }
}
