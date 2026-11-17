using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PandaBear.Npgsql.Tests;

/// <summary>A hosted service that records the logger providers of the application it runs in and logs <see cref="Entry"/> when it starts.</summary>
internal sealed partial class LoggingProbe : IHostedService
{
    /// <summary>The entry the probe logs when the application starts.</summary>
    internal const string Entry = "The logging probe started.";

    private IServiceProvider? _services;

    /// <summary>Gets the logger providers of the application, once it has started.</summary>
    internal IReadOnlyList<ILoggerProvider> Providers { get; private set; } = [];

    /// <summary>Binds the probe to the services of the application that starts it.</summary>
    /// <param name="services">The application's services.</param>
    /// <returns>The probe.</returns>
    internal LoggingProbe Bind(IServiceProvider services)
    {
        _services = services;
        return this;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var services = _services ?? throw new InvalidOperationException("The probe is not bound to an application.");
        Providers = [.. services.GetServices<ILoggerProvider>()];
        var logger = services.GetRequiredService<ILogger<LoggingProbe>>();
        Started(logger);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = Entry)]
    private static partial void Started(ILogger logger);
}
