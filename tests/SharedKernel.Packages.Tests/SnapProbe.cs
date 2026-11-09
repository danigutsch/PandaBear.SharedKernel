using System.Net.Sockets;

namespace PandaBear.Packages.Tests;

/// <summary>Tells tests whether the real <c>snap</c> is available.</summary>
internal static class SnapProbe
{
    /// <summary>The absolute path of <c>snap</c>.</summary>
    public const string Path = "/usr/bin/snap";

    /// <summary>The socket of the snap daemon.</summary>
    public const string SocketPath = "/run/snapd.socket";

    /// <summary>Gets a value indicating whether <c>snap</c> is installed.</summary>
    public static bool IsInstalled => File.Exists(Path);

    /// <summary>Gets a value indicating whether <c>snap</c> is installed and its daemon accepts a connection within a second.</summary>
    public static bool IsReachable => IsInstalled && CanConnect();

    /// <summary>Returns the names of the snaps that have a <c>current</c> mount under <c>/snap</c>, read without <c>snap</c>.</summary>
    public static IReadOnlyList<string> MountedSnapNames() =>
        [.. Directory.EnumerateDirectories("/snap").Where(static d => Directory.Exists(System.IO.Path.Combine(d, "current"))).Select(static d => System.IO.Path.GetFileName(d))];

    private static bool CanConnect()
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(1));
        try
        {
            using Socket socket = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            socket.ConnectAsync(new UnixDomainSocketEndPoint(SocketPath), timeout.Token).AsTask().GetAwaiter().GetResult();
            return true;
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
