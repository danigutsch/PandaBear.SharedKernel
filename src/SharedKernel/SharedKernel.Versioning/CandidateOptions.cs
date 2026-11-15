namespace PandaBear.Versioning;

/// <summary>What <see cref="CandidateValidator.Create"/> and <see cref="CandidateValidator.Validate"/> pack and validate.</summary>
/// <param name="RepoRoot">The root of a clean, committed SharedKernel checkout.</param>
/// <param name="Version">
/// The unique prerelease package version, or <see langword="null"/> for
/// <c>&lt;base&gt;-alpha.local.t&lt;UTC timestamp&gt;.g&lt;revision&gt;</c>.
/// </param>
/// <param name="BaselineVersion">
/// The published package version to compare APIs against, or <see langword="null"/> for none. It requires
/// <c>PandaBear.SharedKernel.*</c> mapped only to an existing local feed in <c>NuGet.Config</c>.
/// </param>
/// <param name="NativeAot">Whether consumption also publishes and runs the samples with NativeAOT.</param>
public sealed record CandidateOptions(string RepoRoot, string? Version = null, string? BaselineVersion = null, bool NativeAot = false)
{
    /// <summary>Gets the target frameworks to build the consumers for, each in turn. Default: <see cref="ConsumerFrameworks.Default"/>.</summary>
    public IReadOnlyList<string> Frameworks { get; init; } = ConsumerFrameworks.Default;
}
