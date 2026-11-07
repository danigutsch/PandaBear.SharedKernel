namespace PandaBear.Versioning.Tool;

/// <summary>Options for the <c>pack-dev</c> command.</summary>
/// <param name="Feed">The feed directory that receives the packages.</param>
/// <param name="Version">The package version.</param>
/// <param name="RepoRoot">The SharedKernel repository root.</param>
/// <param name="Projects">The projects to rebuild; empty rebuilds every project.</param>
internal sealed record DevPackOptions(string Feed, string Version, string RepoRoot, IReadOnlyList<string> Projects);
