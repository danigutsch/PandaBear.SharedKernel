namespace PandaBear.RepoConfig.Tool;

/// <summary>One parsed entry of git status porcelain v2 output.</summary>
/// <param name="RecordType">Porcelain record type marker.</param>
/// <param name="Status">Two-character index and work-tree status code.</param>
/// <param name="Submodule">Submodule state field.</param>
/// <param name="HeadMode">File mode in HEAD.</param>
/// <param name="IndexMode">File mode in the index.</param>
/// <param name="WorkTreeMode">File mode in the work tree.</param>
/// <param name="Path">Repository-relative path of the entry.</param>
/// <param name="OriginalPath">Source path for renames and copies, otherwise null.</param>
internal sealed record RoadmapGitStatusEntry(
    string RecordType,
    string Status,
    string Submodule,
    string HeadMode,
    string IndexMode,
    string WorkTreeMode,
    string Path,
    string? OriginalPath);
