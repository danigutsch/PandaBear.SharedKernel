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

    /// <summary>
    /// Diagnostic emitted when an xUnit test method does not follow the repository underscore naming convention.
    /// </summary>
    public const string XunitTestMethodNaming = "SKTEST002";
}
