namespace PandaBear.Versioning;

/// <summary>Which candidate <see cref="CandidateValidator.Consume"/> consumes and where it records the result.</summary>
/// <param name="RepoRoot">The SharedKernel repository root, whose samples consume the packages.</param>
/// <param name="Manifest">The candidate manifest, <c>candidate.json</c>, beside the packages it lists.</param>
/// <param name="Destination">The evidence file to write, which may be <paramref name="Manifest"/> itself.</param>
/// <param name="NativeAot">Whether to also publish and run the samples with NativeAOT.</param>
public sealed record CandidateConsumptionOptions(string RepoRoot, string Manifest, string Destination, bool NativeAot = false)
{
    /// <summary>Gets the target frameworks to build the consumers for, each in turn. Default: the frameworks <see cref="RepositoryFrameworks.Read"/> reads from the repository root.</summary>
    public IReadOnlyList<string>? Frameworks { get; init; }
}
