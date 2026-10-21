namespace PandaBear.Style.Analyzers;

/// <summary>
/// Exposes the public diagnostic identifiers for repository style analyzers.
/// </summary>
public static class StyleDiagnosticIds
{
    /// <summary>Diagnostic for direct wall-clock reads.</summary>
    public const string DirectCurrentTime = "SKSTYLE014";

    /// <summary>
    /// Diagnostic emitted when a method name ends with <c>Async</c> without requiring that suffix.
    /// </summary>
    public const string AsyncSuffix = "SKSTYLE001";

    /// <summary>
    /// Diagnostic emitted when a <see cref="CancellationToken"/> parameter is not named <c>ct</c>.
    /// </summary>
    public const string CancellationTokenParameterName = "SKSTYLE002";

    /// <summary>
    /// Diagnostic emitted when a <see cref="CancellationToken"/> parameter declares a default value.
    /// </summary>
    public const string CancellationTokenDefaultValue = "SKSTYLE003";

    /// <summary>
    /// Diagnostic emitted when a source file declares more than one top-level type.
    /// </summary>
    public const string MultipleTopLevelTypesPerFile = "SKSTYLE004";

    /// <summary>
    /// Diagnostic emitted when a generic type name includes a suffix that repeats its generic arity.
    /// </summary>
    public const string GenericTypeNameSuffix = "SKSTYLE005";

    /// <summary>
    /// Diagnostic emitted when a catch filter suppresses all <see cref="OperationCanceledException" /> values.
    /// </summary>
    public const string BroadOperationCanceledExceptionFilter = "SKSTYLE006";

    /// <summary>
    /// Diagnostic emitted when production code uses direct logger calls instead of source-generated logging methods.
    /// </summary>
    public const string NonSourceGeneratedLogging = "SKSTYLE007";

    /// <summary>
    /// Diagnostic emitted when a domain event type does not end with <c>DomainEvent</c>.
    /// </summary>
    public const string DomainEventSuffix = "SKSTYLE008";

    /// <summary>
    /// Diagnostic emitted when a method returns only successful <c>PandaBear.Results.Result</c> values.
    /// </summary>
    public const string SuccessOnlyResultMethod = "SKSTYLE009";

    /// <summary>
    /// Diagnostic emitted when a method uses <c>Result&lt;T&gt;</c> only to distinguish a value from not found.
    /// </summary>
    public const string OptionalResultMethod = "SKSTYLE010";

    /// <summary>
    /// Diagnostic emitted for whole-input reads in explicitly enabled size-sensitive scopes.
    /// </summary>
    public const string UnboundedWholeRead = "SKSTYLE011";

    /// <summary>Diagnostic emitted for a registered direct string literal.</summary>
    public const string GovernedLiteral = "SKSTYLE012";

    /// <summary>Diagnostic emitted for invalid governed-literal metadata.</summary>
    public const string InvalidGovernedLiteralCatalog = "SKSTYLE013";
}
