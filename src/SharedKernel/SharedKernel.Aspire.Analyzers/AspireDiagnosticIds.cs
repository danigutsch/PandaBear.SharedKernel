namespace SharedKernel.Aspire.Analyzers;

/// <summary>
/// Exposes the public diagnostic identifiers for SharedKernel Aspire analyzers.
/// </summary>
public static class AspireDiagnosticIds
{
    /// <summary>
    /// Diagnostic emitted when Aspire container image tags and digests are not pinned together.
    /// </summary>
    public const string ImageTagAndDigest = "SKASPIRE001";
}
