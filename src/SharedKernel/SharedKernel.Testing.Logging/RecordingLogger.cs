using Microsoft.Extensions.Logging;

namespace PandaBear.Testing.Logging;

/// <summary>A fake logger that keeps every record in memory so tests can assert on what was logged.</summary>
public class RecordingLogger : ILogger
{
    private readonly Lock _gate = new();
    private readonly List<RecordedLog> _records = [];

    /// <summary>Gets a snapshot of the captured records in logging order.</summary>
    public IReadOnlyList<RecordedLog> Records
    {
        get
        {
            lock (_gate)
            {
                return [.. _records];
            }
        }
    }

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
        lock (_gate)
        {
            _records.Add(new RecordedLog(logLevel, eventId, formatter(state, exception), values, exception));
        }
    }
}
