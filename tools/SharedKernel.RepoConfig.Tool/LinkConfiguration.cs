namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// The documentation files that <c>verify-links</c> scans.
/// </summary>
/// <param name="Include">Path patterns of the files to scan when no path is given.</param>
/// <param name="Exclude">Path patterns of files that are never scanned, matched against the path after symbolic links resolve.</param>
internal sealed record LinkConfiguration(string[] Include, string[] Exclude);
