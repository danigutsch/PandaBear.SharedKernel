using Microsoft.Extensions.Logging;

namespace PandaBear.Blazor.Tests;

/// <summary>One captured log entry.</summary>
/// <param name="Level">The log level.</param>
/// <param name="EventId">The event id.</param>
/// <param name="Message">The formatted message.</param>
/// <param name="Exception">The exception argument passed to the logger.</param>
/// <param name="Values">The structured state values.</param>
internal sealed record CapturedLogEntry(
    LogLevel Level,
    EventId EventId,
    string Message,
    Exception? Exception,
    IReadOnlyDictionary<string, string?> Values);
