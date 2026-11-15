namespace PandaBear.Versioning;

/// <summary>What <see cref="SharedKernelPackager.ConsumePackages"/> consumes and how.</summary>
/// <param name="RepoRoot">The SharedKernel repository root, whose <c>global.json</c>, central package versions, and samples the check uses.</param>
/// <param name="Feed">The directory that holds the packages.</param>
/// <param name="Version">The package version.</param>
/// <param name="NativeAot">Whether to also publish and run the samples with NativeAOT.</param>
public sealed record PackageConsumptionOptions(string RepoRoot, string Feed, string Version, bool NativeAot = false)
{
    /// <summary>Gets the target frameworks to build the consumers for, each in turn. Default: the frameworks <see cref="RepositoryFrameworks.Read"/> reads from the repository root.</summary>
    public IReadOnlyList<string>? Frameworks { get; init; }
}
