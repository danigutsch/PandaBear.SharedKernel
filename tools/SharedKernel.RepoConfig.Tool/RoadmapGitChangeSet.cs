namespace PandaBear.RepoConfig.Tool;

/// <summary>Working-tree changes captured from git status, with a digest binding their contents.</summary>
/// <param name="HasChanges">Whether any entry was reported.</param>
/// <param name="Digest">Digest over the changed entries and file contents.</param>
/// <param name="Paths">Distinct changed paths, including rename origins, in ordinal order.</param>
/// <param name="Entries">Parsed git status entries.</param>
internal sealed record RoadmapGitChangeSet(
    bool HasChanges,
    string Digest,
    IReadOnlyList<string> Paths,
    IReadOnlyList<RoadmapGitStatusEntry> Entries);
