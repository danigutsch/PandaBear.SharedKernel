using Microsoft.Extensions.Logging;

namespace PandaBear.Testing.Logging;

/// <summary>A captured log record: level, identifier, rendered message, structured state and attached exception.</summary>
/// <param name="Level">The log level.</param>
/// <param name="EventId">The event identifier.</param>
/// <param name="Message">The rendered message.</param>
/// <param name="State">The structured state values.</param>
/// <param name="Exception">The exception passed to the logger, if any.</param>
public sealed record RecordedLog(
    LogLevel Level,
    EventId EventId,
    string Message,
    IReadOnlyList<KeyValuePair<string, object?>> State,
    Exception? Exception);
