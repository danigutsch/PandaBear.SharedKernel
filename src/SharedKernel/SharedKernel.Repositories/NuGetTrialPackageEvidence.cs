namespace SharedKernel.Repositories;

/// <summary>Shows how a package affected by the trial is selected before any edits.</summary>
/// <param name="Package">Package identifier.</param>
/// <param name="Targets">Evaluated project and target selections, with version and classification.</param>
/// <param name="DependencyPaths">The SDK's requesting dependency paths, when available.</param>
internal sealed record NuGetTrialPackageEvidence(string Package, IReadOnlyList<string> Targets, string? DependencyPaths);
