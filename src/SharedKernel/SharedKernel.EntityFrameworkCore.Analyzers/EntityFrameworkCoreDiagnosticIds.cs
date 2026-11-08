namespace PandaBear.EntityFrameworkCore.Analyzers;

/// <summary>
/// Exposes the public diagnostic identifiers for SharedKernel Entity Framework Core analyzers.
/// </summary>
public static class EntityFrameworkCoreDiagnosticIds
{
    /// <summary>
    /// Diagnostic emitted when a unique index is declared without an application rule (<c>HasUniqueRule</c>).
    /// </summary>
    public const string UntaggedUniqueIndex = "SKEF001";
}
