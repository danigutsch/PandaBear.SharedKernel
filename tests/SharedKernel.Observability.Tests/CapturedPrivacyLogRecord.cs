namespace PandaBear.Observability.Tests;

internal sealed record CapturedPrivacyLogRecord(
    string? Body,
    string? FormattedMessage,
    Exception? Exception,
    IReadOnlyList<KeyValuePair<string, object?>> Attributes,
    int ScopeCount);
