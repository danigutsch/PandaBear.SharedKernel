using Microsoft.Extensions.Logging;

namespace PandaBear.AspNetCore.Tests;

/// <summary>A captured log record: identifier, rendered message, structured state and attached exception.</summary>
/// <param name="Level">The log level.</param>
/// <param name="EventId">The event identifier.</param>
/// <param name="Message">The rendered message.</param>
/// <param name="State">The structured state values.</param>
/// <param name="Exception">The exception passed to the logger, if any.</param>
internal sealed record RecordedLog(LogLevel Level, EventId EventId, string Message, IReadOnlyList<KeyValuePair<string, object?>> State, Exception? Exception);

/// <summary>A fake logger that keeps every record.</summary>
/// <typeparam name="T">The logger category.</typeparam>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<RecordedLog> _records = [];

    /// <summary>Gets the captured records.</summary>
    public IReadOnlyList<RecordedLog> Records => _records;

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        var values = state is IReadOnlyList<KeyValuePair<string, object?>> list ? list.ToArray() : [];
        lock (_records)
        {
            _records.Add(new RecordedLog(logLevel, eventId, formatter(state, exception), values, exception));
        }
    }
}
