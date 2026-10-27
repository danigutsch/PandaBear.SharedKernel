namespace PandaBear.Versioning.Tool;

/// <summary>Arguments of a SharedKernel pack.</summary>
/// <param name="Version">Package version.</param>
/// <param name="OutputRoot">Directory under which the package folder is created.</param>
/// <param name="VerifyRestore">Whether to restore from the packed local feed to prove it is consumable.</param>
/// <param name="RepoRoot">Repository root.</param>
/// <param name="AssemblyVersion">Assembly version override, or null.</param>
/// <param name="FileVersion">File version override, or null.</param>
/// <param name="InformationalVersion">Informational version override, or null.</param>
/// <param name="PackageValidationBaselineVersion">Published version for package validation, or null to skip it.</param>
internal sealed record PackSharedKernelOptions(
    string Version,
    string OutputRoot,
    bool VerifyRestore,
    string RepoRoot,
    string? AssemblyVersion = null,
    string? FileVersion = null,
    string? InformationalVersion = null,
    string? PackageValidationBaselineVersion = null);
