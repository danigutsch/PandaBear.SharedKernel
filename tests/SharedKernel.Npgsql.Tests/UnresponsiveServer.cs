using System.Net;
using System.Net.Sockets;

namespace PandaBear.Npgsql.Tests;

/// <summary>
/// Accepts every connection on a loopback port and never answers, like a PostgreSQL server whose process is paused or whose
/// network drops packets: Npgsql connects, sends its startup message, and waits until its connection timeout ends.
/// </summary>
internal sealed class UnresponsiveServer : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly List<TcpClient> _accepted = [];
    private readonly Task _accepting;

    /// <summary>Starts listening on a free loopback port.</summary>
    public UnresponsiveServer()
    {
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _accepting = AcceptAll();
    }

    /// <summary>Gets the port on which the server listens.</summary>
    public int Port
    {
        get;
    }

    /// <summary>Gets how many connections the server accepted so far.</summary>
    public int Accepted
    {
        get
        {
            lock (_accepted)
            {
                return _accepted.Count;
            }
        }
    }

    /// <summary>Creates a connection string for the server whose connection attempt times out after <paramref name="timeoutSeconds"/>.</summary>
    /// <param name="timeoutSeconds">The Npgsql connection timeout.</param>
    /// <param name="password">The password, which no message may repeat.</param>
    public string ConnectionString(int timeoutSeconds, string password) =>
        $"Host=127.0.0.1;Port={Port};Username=postgres;Password={password};Timeout={timeoutSeconds};Pooling=false";

    /// <inheritdoc />
    public void Dispose()
    {
        _listener.Dispose();
        _ = _accepting.Wait(TimeSpan.FromSeconds(30));
        lock (_accepted)
        {
            _accepted.ForEach(static client => client.Dispose());
        }
    }

    private async Task AcceptAll()
    {
        try
        {
            while (true)
            {
                var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                lock (_accepted)
                {
                    _accepted.Add(client);
                }
            }
        }
        catch (Exception exception) when (exception is ObjectDisposedException or SocketException or InvalidOperationException)
        {
            // The listener stopped.
        }
    }
}
