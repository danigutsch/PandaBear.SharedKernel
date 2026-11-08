namespace PandaBear.Results.Analyzers;

/// <summary>
/// Exposes the public diagnostic identifiers for SharedKernel Results analyzers.
/// </summary>
public static class ResultsDiagnosticIds
{
    /// <summary>
    /// Diagnostic emitted when a minimal API endpoint delegate returns a Result or Result&lt;T&gt; directly.
    /// </summary>
    public const string RawResultEndpoint = "SKRES001";

    /// <summary>
    /// Diagnostic emitted when the Result returned by a call is discarded.
    /// </summary>
    public const string DiscardedResult = "SKRES002";
}
