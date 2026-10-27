namespace PandaBear.RepoConfig.Tool;

internal sealed record DependencySubmitOptions(
    string RootPath,
    string SnapshotPath,
    string Repository,
    bool Force);
