using Microsoft.Extensions.Logging;

namespace PandaBear.AspNetCore.Tests;

/// <summary>A logger whose every call throws, to prove telemetry never breaks the request flow.</summary>
/// <typeparam name="T">The logger category.</typeparam>
internal sealed class ThrowingLogger<T> : ILogger<T>
{
    /// <summary>The message of the thrown exception; it must never be surfaced.</summary>
    public const string Secret = "logger-secret";

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        throw new InvalidOperationException(Secret);
}
