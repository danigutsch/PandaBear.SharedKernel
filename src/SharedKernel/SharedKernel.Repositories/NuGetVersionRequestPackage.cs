namespace SharedKernel.Repositories;

/// <summary>One direct owner's evaluated source scopes and versions before the trial.</summary>
/// <param name="Package">The direct package ID.</param>
/// <param name="Current">The resolved direct versions the candidate must exceed.</param>
/// <param name="Projects">The sources of every evaluated project directly using the owner.</param>
internal sealed record NuGetVersionRequestPackage(string Package, string[] Current, NuGetVersionSources[] Projects);
