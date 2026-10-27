namespace PandaBear.RepoConfig.Tool;

/// <summary>Settings for submitting a dependency snapshot.</summary>
/// <param name="RootPath">Repository root.</param>
/// <param name="SnapshotPath">Path of the snapshot file to submit.</param>
/// <param name="Repository">Target repository in <c>owner/name</c> form.</param>
/// <param name="Force">Whether to post the snapshot instead of only previewing the submission.</param>
internal sealed record DependencySubmitOptions(
    string RootPath,
    string SnapshotPath,
    string Repository,
    bool Force);
