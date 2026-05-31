namespace SharedKernel.Style.Analyzers;

/// <summary>
/// Exposes the public diagnostic identifiers for repository style analyzers.
/// </summary>
public static class StyleDiagnosticIds
{
    /// <summary>
    /// Diagnostic emitted when a method name ends with <c>Async</c> without requiring that suffix.
    /// </summary>
    public const string AsyncSuffix = "SKSTYLE001";
}
