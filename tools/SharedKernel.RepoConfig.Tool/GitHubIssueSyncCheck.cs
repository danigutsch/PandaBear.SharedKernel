namespace PandaBear.RepoConfig.Tool;

internal sealed record GitHubIssueSyncCheck(IReadOnlyList<string> ExtraLabels, string? Diagnostic);
