using Microsoft.Extensions.Logging.Abstractions;

namespace SharedKernel.Testing.AspNetCore.TestApp;

/// <summary>Observes when the service provider created by the worker fixture releases its services.</summary>
internal sealed class WorkerFixtureLoggerProvider(Action onDispose) : ILoggerProvider
{
    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => NullLogger.Instance;

    /// <inheritdoc />
    public void Dispose() => onDispose();
}
