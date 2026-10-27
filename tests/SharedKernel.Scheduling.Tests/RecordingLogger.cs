using Microsoft.Extensions.Logging;

namespace PandaBear.Scheduling.Tests;

/// <summary>
/// Logger that keeps every formatted message with its level in memory so tests can assert on what was logged.
/// </summary>
internal sealed class RecordingLogger : ILogger
{
    private readonly List<(LogLevel Level, string Message)> entries = [];

    /// <summary>The level and formatted message of each entry logged so far, in order.</summary>
    public IReadOnlyList<(LogLevel Level, string Message)> Entries => entries;

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        entries.Add((logLevel, formatter(state, exception)));
    }
}
