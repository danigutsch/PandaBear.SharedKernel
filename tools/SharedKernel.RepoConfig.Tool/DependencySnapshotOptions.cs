namespace PandaBear.RepoConfig.Tool;

internal sealed record DependencySnapshotOptions(
    string RootPath,
    string Sha,
    string Reference,
    string Job,
    string Correlator,
    string OutputPath);
