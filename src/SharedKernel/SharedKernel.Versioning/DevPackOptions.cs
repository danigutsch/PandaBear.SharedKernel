namespace PandaBear.Versioning;

/// <summary>What <see cref="SharedKernelPackager.PackDev"/> packs, where, and which NuGet cache it evicts the packages from.</summary>
/// <param name="Feed">The feed directory that receives the packages; created when missing.</param>
/// <param name="Version">The package version, such as <see cref="DefaultVersion"/>.</param>
/// <param name="RepoRoot">The SharedKernel repository root.</param>
/// <param name="Projects">The projects to rebuild, such as <c>SharedKernel.Results</c>; empty rebuilds every project.</param>
/// <param name="ConsumerDirectory">
/// The consumer directory whose NuGet configuration decides the global packages folder, usually the host's current directory.
/// </param>
/// <param name="NuGetPackages">
/// The value of the <c>NUGET_PACKAGES</c> environment variable, which the host reads, or <see langword="null"/> when it is not set.
/// </param>
public sealed record DevPackOptions(
    string Feed,
    string Version,
    string RepoRoot,
    IReadOnlyList<string> Projects,
    string ConsumerDirectory,
    string? NuGetPackages)
{
    /// <summary>The package version of a dev pack when none is given.</summary>
    public const string DefaultVersion = "0.1.0-dev.local";
}
