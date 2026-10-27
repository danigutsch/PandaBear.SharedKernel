using System.Net.Sockets;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aspire.Hosting;

/// <summary>
/// Health check that sends a PING to the clamd daemon and expects PONG, so the resource reports ready only when the scanner answers.
/// </summary>
/// <param name="endpoint">Endpoint of the clamd TCP listener.</param>
internal sealed class ClamAvPingHealthCheck(EndpointReference endpoint) : IHealthCheck
{
    /// <summary>Null-terminated clamd command that asks the daemon to reply with PONG.</summary>
    private static ReadOnlyMemory<byte> PingCommand { get; } = "zPING\0"u8.ToArray();

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var endpointValue = await endpoint.GetValueAsync(cancellationToken).ConfigureAwait(false);
        if (!Uri.TryCreate(endpointValue, UriKind.Absolute, out var uri))
        {
            return HealthCheckResult.Unhealthy("ClamAV endpoint is unavailable.");
        }

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(uri.Host, uri.Port, cancellationToken).ConfigureAwait(false);
            using var stream = client.GetStream();
            await stream.WriteAsync(PingCommand, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            var response = new byte[5];
            await stream.ReadExactlyAsync(response, cancellationToken).ConfigureAwait(false);
            return response.AsSpan().SequenceEqual("PONG\0"u8)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("ClamAV returned an unexpected health response.");
        }
        catch (SocketException)
        {
            return HealthCheckResult.Unhealthy("ClamAV endpoint is unavailable.");
        }
        catch (IOException)
        {
            return HealthCheckResult.Unhealthy("ClamAV endpoint is unavailable.");
        }
    }
}
