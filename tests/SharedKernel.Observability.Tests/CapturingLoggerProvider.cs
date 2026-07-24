using Microsoft.Extensions.Logging;

namespace SharedKernel.Observability.Tests;

internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly List<string> messages = [];
    private readonly List<KeyValuePair<string, string?>> structuredValues = [];

    public IReadOnlyList<string> Messages => messages;

    public IReadOnlyList<KeyValuePair<string, string?>> StructuredValues => structuredValues;

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(messages, structuredValues);

    public void Dispose()
    {
    }
}
