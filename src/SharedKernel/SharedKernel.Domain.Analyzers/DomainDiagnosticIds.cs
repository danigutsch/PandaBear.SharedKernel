namespace PandaBear.Domain.Analyzers;

/// <summary>
/// Exposes the public diagnostic identifiers for SharedKernel domain-model analyzers.
/// </summary>
public static class DomainDiagnosticIds
{
    /// <summary>
    /// Diagnostic emitted when a domain-model property exposes a non-private, non-init setter.
    /// </summary>
    public const string NonPrivateSetter = "SKDOM001";

    /// <summary>
    /// Diagnostic emitted when a domain-model property exposes a mutable collection outside its assembly.
    /// </summary>
    public const string MutableCollectionExposure = "SKDOM002";
}
