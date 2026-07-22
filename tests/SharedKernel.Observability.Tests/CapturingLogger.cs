using Microsoft.Extensions.Logging;

namespace SharedKernel.Observability.Tests;

internal sealed class CapturingLogger(
    List<string> messages,
    List<KeyValuePair<string, string?>> structuredValues) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

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
