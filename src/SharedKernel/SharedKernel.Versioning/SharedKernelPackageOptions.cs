namespace SharedKernel.Versioning;

/// <summary>
/// Defines inputs for SharedKernel package planning.
/// </summary>
/// <param name="Version">The package version.</param>
/// <param name="OutputRoot">The package output root.</param>
/// <param name="RepoRoot">The repository root.</param>
/// <param name="AssemblyVersion">The optional assembly version.</param>
/// <param name="FileVersion">The optional file version.</param>
/// <param name="InformationalVersion">The optional informational version.</param>
public sealed record SharedKernelPackageOptions(
    string Version,
    string OutputRoot,
    string RepoRoot,
    string? AssemblyVersion = null,
    string? FileVersion = null,
    string? InformationalVersion = null);
