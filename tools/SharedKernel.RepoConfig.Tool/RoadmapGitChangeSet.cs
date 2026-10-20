namespace SharedKernel.RepoConfig.Tool;

internal sealed record RoadmapGitChangeSet(
    bool HasChanges,
    string Digest,
    IReadOnlyList<string> Paths,
    IReadOnlyList<RoadmapGitStatusEntry> Entries);
