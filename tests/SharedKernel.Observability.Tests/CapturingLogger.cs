using Microsoft.Extensions.Logging;

namespace PandaBear.Observability.Tests;

/// <summary>Logger that records every formatted message and every structured key/value pair into caller-supplied lists.</summary>
/// <param name="messages">List receiving each formatted log message.</param>
/// <param name="structuredValues">List receiving each structured state key with its value converted to a string.</param>
internal sealed class CapturingLogger(
    List<string> messages,
    List<KeyValuePair<string, string?>> structuredValues) : ILogger
{
    /// <summary>Ignores the scope and returns no disposable.</summary>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <summary>Reports every log level as enabled.</summary>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <summary>Adds the formatted message to the messages list and, when the state is a key/value sequence, each pair to the structured values list.</summary>
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        messages.Add(formatter(state, exception));
        if (state is IEnumerable<KeyValuePair<string, object?>> values)
        {
            foreach (var value in values)
            {
                structuredValues.Add(new KeyValuePair<string, string?>(value.Key, value.Value?.ToString()));
            }
        }
    }
}
