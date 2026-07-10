namespace SharedKernel.Versioning;

/// <summary>
/// Represents a package resolved from one or more lock files.
/// </summary>
/// <param name="Id">The package ID.</param>
/// <param name="Version">The resolved package version.</param>
/// <param name="LockFiles">Repository-relative lock files that resolve the package.</param>
public sealed record ResolvedNuGetPackage(string Id, string Version, IReadOnlyList<string> LockFiles);
