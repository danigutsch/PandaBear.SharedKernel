namespace SharedKernel.RepoConfig.Tool;

internal sealed record RoadmapGitStatusEntry(
    string RecordType,
    string Status,
    string Submodule,
    string HeadMode,
    string IndexMode,
    string WorkTreeMode,
    string Path,
    string? OriginalPath);
