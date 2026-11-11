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
}
