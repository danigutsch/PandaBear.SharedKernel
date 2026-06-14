namespace SharedKernel.Testing.Analyzers;

/// <summary>
/// Exposes the public diagnostic identifiers for repository testing analyzers.
/// </summary>
public static class TestingDiagnosticIds
{
    /// <summary>
    /// Diagnostic emitted when a test method uses local pragma warning suppression directives.
    /// </summary>
    public const string TestMethodWarningSuppression = "SKTEST001";
}
