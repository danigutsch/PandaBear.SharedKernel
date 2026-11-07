namespace PandaBear.Blazor.Analyzers;

/// <summary>
/// Exposes the public diagnostic identifiers for SharedKernel Blazor analyzers.
/// </summary>
public static class BlazorDiagnosticIds
{
    /// <summary>
    /// Diagnostic emitted when a component sets a flag to <c>true</c>, awaits, and resets it without <c>try</c>/<c>finally</c>.
    /// </summary>
    public const string UnguardedBusyFlag = "SKBLZ001";

    /// <summary>
    /// Diagnostic emitted when a component injects a service that goes stale in a connection-long service scope.
    /// </summary>
    public const string LongLivedScopeService = "SKBLZ002";

    /// <summary>
    /// Diagnostic emitted when a component loop renders table rows, list items or components without a key.
    /// </summary>
    public const string MissingKey = "SKBLZ003";
}
