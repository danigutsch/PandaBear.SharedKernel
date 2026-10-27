namespace PandaBear.Observability.Tests;

/// <summary>Snapshot of one log record as seen by the exporter after privacy processing.</summary>
/// <param name="Body">Message template remaining on the record, or null when absent.</param>
/// <param name="FormattedMessage">Rendered message, or null when the processor stripped it.</param>
/// <param name="Exception">Exception still attached to the record, or null when it was removed.</param>
/// <param name="Attributes">Structured attributes that survived redaction.</param>
/// <param name="ScopeCount">Number of scopes still attached to the record.</param>
internal sealed record CapturedPrivacyLogRecord(
    string? Body,
    string? FormattedMessage,
    Exception? Exception,
    IReadOnlyList<KeyValuePair<string, object?>> Attributes,
    int ScopeCount);
