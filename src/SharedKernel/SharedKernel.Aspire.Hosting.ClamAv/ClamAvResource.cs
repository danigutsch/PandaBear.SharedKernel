using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Represents a private ClamAV daemon container.
/// </summary>
public sealed class ClamAvResource(string name) : ContainerResource(name)
{
    /// <summary>
    /// Gets the private ClamAV TCP endpoint name.
    /// </summary>
    public const string TcpEndpointName = "tcp";
}
