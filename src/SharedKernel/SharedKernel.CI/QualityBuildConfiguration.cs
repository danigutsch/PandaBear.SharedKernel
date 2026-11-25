namespace SharedKernel.CI;

/// <summary>Identifies additional solution projects for repository-wide checks in a partial build.</summary>
/// <param name="GovernedPackageIdPrefix">The package ID prefix of the libraries checked for governed literals.</param>
/// <param name="Projects">The repository-relative solution projects that run the checks.</param>
public sealed record QualityBuildConfiguration(string GovernedPackageIdPrefix, IReadOnlyList<string> Projects);
