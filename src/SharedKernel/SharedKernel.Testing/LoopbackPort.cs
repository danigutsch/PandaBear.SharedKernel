using System.Net;
using System.Net.Sockets;

namespace PandaBear.Testing;

/// <summary>Finds loopback ports for tests.</summary>
public static class LoopbackPort
{
    /// <summary>Returns a loopback port on which nothing listens, because the listener that found it is already closed.</summary>
    /// <returns>A TCP port on the IPv4 loopback address that was free when it was found.</returns>
    public static int GetFree()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
