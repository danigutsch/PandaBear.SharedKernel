namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// A repository-local Git setting that <c>baseline apply</c> sets with <c>git config --local</c>.
/// </summary>
/// <param name="Key">An allowlisted key, such as <c>pull.ff</c>.</param>
/// <param name="Value">The value: 1 to 200 characters without control characters, not starting with <c>-</c>.</param>
/// <param name="OverwriteExisting">Allows replacing a different value that the repository already has.</param>
internal sealed record BaselineGitConfigurationEntry(string Key, string Value, bool OverwriteExisting = false);
