using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PandaBear.K6.Tool.Tests;

/// <summary>A loopback HTTP listener that answers every request with one status code and records request lines.</summary>
internal sealed class HealthServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _accepting;
    private readonly int _status;

    /// <summary>Starts listening on an ephemeral loopback port that answers every request with <c>status</c>.</summary>
    public HealthServer(int status = 200)
    {
        _status = status;
        _listener.Start();
        _accepting = Accept();
    }

    /// <summary>The loopback port the listener is bound to.</summary>
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>The request line of each request received, in arrival order.</summary>
    public ConcurrentQueue<string> Requests { get; } = new();

    private async Task Accept()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            using (client)
            {
                var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                Requests.Enqueue(await reader.ReadLineAsync(_stop.Token) ?? string.Empty);
                while (!string.IsNullOrEmpty(await reader.ReadLineAsync(_stop.Token)))
                {
                }

                var response = string.Create(CultureInfo.InvariantCulture, $"HTTP/1.1 {_status} Status\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(Encoding.ASCII.GetBytes(response), _stop.Token);
            }
        }
    }

    /// <summary>Stops accepting connections and waits for the accept loop to finish.</summary>
    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        _listener.Dispose();
        await _accepting;
        _stop.Dispose();
    }
}
