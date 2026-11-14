using Microsoft.Extensions.Hosting;

namespace PandaBear.Testing.AspNetCore.Tests;

/// <summary>A hosted service that records whether the host started it.</summary>
internal sealed class RecordingHostedService : IHostedService
{
    /// <summary>Gets a value indicating whether the host started the service.</summary>
    internal static bool Started
    {
        get;
        private set;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        Started = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
