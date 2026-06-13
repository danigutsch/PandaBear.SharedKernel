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
    /// Diagnostic emitted when a <see cref="System.Threading.CancellationToken"/> parameter is not named <c>ct</c>.
    /// </summary>
    public const string CancellationTokenParameterName = "SKSTYLE002";

    /// <summary>
    /// Diagnostic emitted when a <see cref="System.Threading.CancellationToken"/> parameter declares a default value.
    /// </summary>
    public const string CancellationTokenDefaultValue = "SKSTYLE003";
}
