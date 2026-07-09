using Microsoft.Extensions.Logging;

namespace SharedKernel.Observability.Tests;

internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly List<string> messages = [];

    public IReadOnlyList<string> Messages => messages;

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(messages);

    public void Dispose()
    {
    }
}
