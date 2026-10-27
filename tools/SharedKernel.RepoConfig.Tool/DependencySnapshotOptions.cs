namespace PandaBear.RepoConfig.Tool;

/// <summary>Settings for creating a dependency snapshot.</summary>
/// <param name="RootPath">Repository root to scan.</param>
/// <param name="Sha">Commit SHA the snapshot describes.</param>
/// <param name="Reference">Git reference the snapshot describes.</param>
/// <param name="Job">Name of the job that created the snapshot.</param>
/// <param name="Correlator">Identifier that groups snapshots from the same job.</param>
/// <param name="OutputPath">Path of the snapshot file to write.</param>
internal sealed record DependencySnapshotOptions(
    string RootPath,
    string Sha,
    string Reference,
    string Job,
    string Correlator,
    string OutputPath);
