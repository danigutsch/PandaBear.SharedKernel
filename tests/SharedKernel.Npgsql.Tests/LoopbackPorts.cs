using System.Net;
using System.Net.Sockets;

namespace PandaBear.Npgsql.Tests;

/// <summary>Helpers for finding loopback ports in tests.</summary>
internal static class LoopbackPorts
{
    /// <summary>Returns a loopback port on which nothing listens, because the listener that found it is already closed.</summary>
    public static int Unused()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
