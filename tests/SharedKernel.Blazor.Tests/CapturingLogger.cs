using Microsoft.Extensions.Logging;

namespace PandaBear.Blazor.Tests;

/// <summary>Captures log entries, including the structured state and the exception argument.</summary>
/// <typeparam name="T">The logger category type.</typeparam>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    /// <summary>Gets the captured entries in order.</summary>
    public List<CapturedLogEntry> Entries { get; } = [];

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var values = state is IEnumerable<KeyValuePair<string, object?>> pairs
            ? pairs.ToDictionary(pair => pair.Key, pair => pair.Value?.ToString(), StringComparer.Ordinal)
            : [];
        Entries.Add(new CapturedLogEntry(logLevel, eventId, formatter(state, exception), exception, values));
    }
}
