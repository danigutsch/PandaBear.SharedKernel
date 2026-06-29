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
    /// Diagnostic emitted when Aspire container image tags and digests are not pinned together.
    /// </summary>
    public const string AspireImageTagAndDigest = "SKSTYLE005";

    /// <summary>
    /// Diagnostic emitted when a catch filter suppresses all <see cref="OperationCanceledException" /> values.
    /// </summary>
    public const string BroadOperationCanceledExceptionFilter = "SKSTYLE006";

}
