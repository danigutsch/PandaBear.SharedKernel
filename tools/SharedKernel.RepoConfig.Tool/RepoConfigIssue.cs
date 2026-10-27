namespace PandaBear.RepoConfig.Tool;

/// <summary>Validation problem found in a configuration file.</summary>
/// <param name="Path">Relative path of the file.</param>
/// <param name="Message">Description of the problem.</param>
internal sealed record RepoConfigIssue(string Path, string Message);
