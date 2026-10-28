using Microsoft.Extensions.Logging;

namespace PandaBear.Testing.Logging;

/// <summary>A fake logger that keeps every record and begun scope in memory so tests can assert on what was logged.</summary>
public class RecordingLogger : ILogger
{
    private readonly Lock _gate = new();
    private readonly List<RecordedLog> _records = [];
    private readonly List<object> _scopes = [];

    /// <summary>Gets or sets the lowest level <see cref="IsEnabled" /> reports as enabled; records are kept regardless of level.</summary>
    public LogLevel MinimumLevel
    {
        get; set;
    } = LogLevel.Trace;

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

    /// <summary>Gets a snapshot of the state objects passed to <see cref="BeginScope{TState}" /> in call order.</summary>
    public IReadOnlyList<object> Scopes
    {
        get
        {
            lock (_gate)
            {
                return [.. _scopes];
            }
        }
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        lock (_gate)
        {
            _scopes.Add(state);
        }

        return null;
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => logLevel >= MinimumLevel;

    /// <summary>Records the call. The structured state is kept when it is a sequence of key/value pairs and is empty otherwise.</summary>
    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        var values = state is IEnumerable<KeyValuePair<string, object?>> pairs ? pairs.ToArray() : [];
        lock (_gate)
        {
            _records.Add(new RecordedLog(logLevel, eventId, formatter(state, exception), values, exception));
        }
    }
}
