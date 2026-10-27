using System.Text;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Builds NUL-terminated <c>git status --porcelain=v2 -z</c> records for tests.</summary>
internal static class RoadmapGitStatusTestData
{
    private const string Hash = "0000000000000000000000000000000000000000";

    /// <summary>Builds an ordinary changed-entry record (<c>1 .M</c>) with the given modes and submodule state.</summary>
    /// <param name="path">The changed path.</param>
    /// <param name="headMode">The file mode in HEAD.</param>
    /// <param name="indexMode">The file mode in the index.</param>
    /// <param name="workTreeMode">The file mode in the working tree.</param>
    /// <param name="submodule">The four-character submodule field.</param>
    public static byte[] Ordinary(
        string path,
        string headMode = "100644",
        string indexMode = "100644",
        string workTreeMode = "100644",
        string submodule = "N...") =>
        Encoding.UTF8.GetBytes($"1 .M {submodule} {headMode} {indexMode} {workTreeMode} {Hash} {Hash} {path}\0");

    /// <summary>Builds a rename record (<c>2 R. ... R100</c>) followed by the original path.</summary>
    /// <param name="path">The new path.</param>
    /// <param name="originalPath">The path before the rename.</param>
    public static byte[] Rename(string path, string originalPath) =>
        Encoding.UTF8.GetBytes($"2 R. N... 100644 100644 100644 {Hash} {Hash} R100 {path}\0{originalPath}\0");

    /// <summary>Builds a copy record (<c>2 C. ... C100</c>) followed by the source path.</summary>
    /// <param name="path">The copy's path.</param>
    /// <param name="originalPath">The path that was copied.</param>
    public static byte[] Copy(string path, string originalPath) =>
        Encoding.UTF8.GetBytes($"2 C. N... 100644 100644 100644 {Hash} {Hash} C100 {path}\0{originalPath}\0");

    /// <summary>Builds an untracked-file record (<c>? path</c>).</summary>
    /// <param name="path">The untracked path.</param>
    public static byte[] Untracked(string path) => Encoding.UTF8.GetBytes($"? {path}\0");
}
