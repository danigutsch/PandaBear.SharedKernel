using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace PandaBear.Npgsql.Tests;

/// <summary>
/// Accepts every connection on a loopback port and never answers, like a PostgreSQL server whose process is paused or whose
/// network drops packets: Npgsql connects, sends its startup message, and waits until its connection timeout ends.
/// </summary>
internal sealed class UnresponsiveServer : IDisposable
{
    // The first message of a PostgreSQL connection is a length followed by a protocol version or a request code.
    private const int StartupMessageProtocol = 196608;
    private const int SslRequestCode = 80877103;
    private const int GssEncryptionRequestCode = 80877104;

    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly List<TcpClient> _accepted = [];
    private readonly Task _accepting;
    private int _handshakes;

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

    /// <summary>
    /// Gets how many connections started a PostgreSQL handshake with a startup message or a request for encryption. A
    /// connection that sends nothing, or asks to cancel another one, is not counted: when its connection timeout ends,
    /// Npgsql itself opens one more connection to the server, which is no second attempt to connect.
    /// </summary>
    public int Handshakes => Volatile.Read(ref _handshakes);

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

                _ = CountHandshake(client);
            }
        }
        catch (Exception exception) when (exception is ObjectDisposedException or SocketException or InvalidOperationException)
        {
            // The listener stopped.
        }
    }

    private async Task CountHandshake(TcpClient client)
    {
        var header = new byte[8];
        try
        {
            var stream = client.GetStream();
            for (var read = 0; read < header.Length;)
            {
                var received = await stream.ReadAsync(header.AsMemory(read)).ConfigureAwait(false);
                if (received == 0)
                {
                    return;
                }

                read += received;
            }

            if (BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(4)) is StartupMessageProtocol or SslRequestCode or GssEncryptionRequestCode)
            {
                _ = Interlocked.Increment(ref _handshakes);
            }
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException or InvalidOperationException or SocketException)
        {
            // The client closed the connection or the server is disposed.
        }
    }
}
