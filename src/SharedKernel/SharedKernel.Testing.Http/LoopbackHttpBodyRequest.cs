// Adapted from ViajantesTurismo under its MIT license. See LICENSE.ViajantesTurismo.txt.

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PandaBear.Testing.Http;

/// <summary>Test client for an HTTP/1.1 loopback request whose declared body can stay incomplete.</summary>
public sealed class LoopbackHttpBodyRequest : IDisposable
{
    private readonly TcpClient _connection = new();

    /// <summary>Opens a connection and writes the request headers and available prefix of its body.</summary>
    /// <param name="request">An HTTP request with an absolute loopback address and the headers to send.</param>
    /// <param name="bodyPrefix">The body bytes to send before waiting or aborting.</param>
    /// <param name="declaredLength">The total length promised in the Content-Length header.</param>
    /// <param name="ct">Cancellation of the connection and write.</param>
    /// <exception cref="ArgumentNullException">The request is null.</exception>
    /// <exception cref="ArgumentException">The address is not an absolute HTTP loopback address, or a header contains a line break.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The declared length is less than the prefix length.</exception>
    public async Task Send(HttpRequestMessage request, ReadOnlyMemory<byte> bodyPrefix, long declaredLength, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThan(declaredLength, bodyPrefix.Length);
        if (request.RequestUri is not { IsAbsoluteUri: true, IsLoopback: true } uri
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal))
        {
            throw new ArgumentException("The request requires an absolute HTTP loopback address.", nameof(request));
        }

        var headers = new StringBuilder();
        headers.Append(CultureInfo.InvariantCulture,
            $"{request.Method} {uri.PathAndQuery} HTTP/1.1\r\nHost: {uri.Authority}\r\nContent-Length: {declaredLength}\r\n");
        AppendHeaders(headers, request.Headers);
        if (request.Content is { } content)
        {
            AppendHeaders(headers, content.Headers);
        }

        headers.Append("\r\n");
        var address = uri.HostNameType == UriHostNameType.Dns ? IPAddress.Loopback : IPAddress.Parse(uri.IdnHost);
        await _connection.ConnectAsync(address, uri.Port, ct).ConfigureAwait(false);
        var stream = _connection.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes(headers.ToString()), ct).ConfigureAwait(false);
        await stream.WriteAsync(bodyPrefix, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Reads the first status line the server sent. Call once per request.</summary>
    /// <param name="ct">Cancellation of the read.</param>
    /// <returns>The status line, or an empty string when the peer closes the connection first.</returns>
    public async Task<string> ReadStatusLine(CancellationToken ct)
    {
        using var reader = new StreamReader(_connection.GetStream(), Encoding.ASCII, leaveOpen: true);
        return await reader.ReadLineAsync(ct).ConfigureAwait(false) ?? string.Empty;
    }

    /// <summary>Closes the connection with a TCP reset to exercise client disconnect behavior.</summary>
    public void Abort()
    {
        _connection.LingerState = new LingerOption(true, 0);
        _connection.Close();
    }

    /// <inheritdoc />
    public void Dispose() => _connection.Dispose();

    private static void AppendHeaders(StringBuilder builder, System.Net.Http.Headers.HttpHeaders headers)
    {
        foreach (var (name, values) in headers.NonValidated)
        {
            if (name.Equals("Host", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var value in values)
            {
                if (value.AsSpan().IndexOfAny('\r', '\n') >= 0)
                {
                    throw new ArgumentException("A request header contains a line break.", nameof(headers));
                }

                builder.Append(name).Append(": ").Append(value).Append("\r\n");
            }
        }
    }
}
