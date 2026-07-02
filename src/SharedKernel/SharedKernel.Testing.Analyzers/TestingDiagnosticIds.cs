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

    /// <summary>
    /// Diagnostic emitted when an xUnit test method is missing configured trait metadata.
    /// </summary>
    public const string XunitTestMethodRequiredTrait = "SKTEST003";

    /// <summary>
    /// Diagnostic emitted when an xUnit test class declares helper members directly.
    /// </summary>
    public const string XunitTestClassHelperMethod = "SKTEST004";

    /// <summary>
    /// Diagnostic emitted when a serial xUnit collection definition is missing a justification.
    /// </summary>
    public const string XunitSerialCollectionJustification = "SKTEST005";

    /// <summary>
    /// Diagnostic emitted when test code calls xUnit assertions directly.
    /// </summary>
    public const string XunitAssertionWrapper = "SKTEST006";

    /// <summary>
    /// Diagnostic emitted when explicit Arrange/Act/Assert markers in an xUnit test method are incomplete or out of order.
    /// </summary>
    public const string XunitArrangeActAssertMarkers = "SKTEST007";

    /// <summary>
    /// Diagnostic emitted when an xUnit test method invokes an explicit cleanup method outside a finally block.
    /// </summary>
    public const string XunitCleanupInvocationFinally = "SKTEST008";
}
