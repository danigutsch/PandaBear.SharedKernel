using Microsoft.Extensions.Logging;

namespace PandaBear.Observability.Tests;

/// <summary>Logger provider whose loggers all write into one shared set of captured messages and structured values.</summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly List<string> messages = [];
    private readonly List<KeyValuePair<string, string?>> structuredValues = [];

    /// <summary>Gets the formatted messages captured by all loggers from this provider.</summary>
    public IReadOnlyList<string> Messages => messages;

    /// <summary>Gets the structured key/value pairs, with values as strings, captured by all loggers from this provider.</summary>
    public IReadOnlyList<KeyValuePair<string, string?>> StructuredValues => structuredValues;

    /// <summary>Returns a <see cref="CapturingLogger"/> writing into the provider's shared lists, regardless of category.</summary>
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(messages, structuredValues);

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
